using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models;
using Bot.Services.Markov;
using Bot.Services.RavenDB;
using Discord;
using Discord.WebSocket;
using Hangfire;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Attachments;
using Raven.Client.Documents.Operations.Attachments;

namespace Bot.Services.Communication.Responders
{
    public sealed class MarkovResponder : MessageResponderBase
    {

        private readonly ConcurrentDictionary<ulong, MarkovServerInstance> chains;
        private readonly List<string> source;
        private readonly Random random;

        internal override bool CanRespondViaPM => true;
        internal override bool CanRespondInNsfw => false;


        public MarkovResponder(
            DiscordSocketClient discordSocketClient,
            RavenDatabaseService ravenDatabaseService,
            ServerConfigurationService serverConfigurationService,
            ILogger<MarkovResponder> logger,
            Random random) : base(discordSocketClient, ravenDatabaseService, serverConfigurationService, logger)
        {
            chains = new ConcurrentDictionary<ulong, MarkovServerInstance>();
            source = new List<string>();
            this.random = random;
        }


        public override async Task InitializeService()
        {
            logger.LogTrace("Querying the database for the markov source file...");
            using (var markovFile = await Raven.GetOrAddDocumentStore("erector_core").Operations.SendAsync(new GetAttachmentOperation(
                documentId: "configuration",
                name: "markov.txt",
                type: AttachmentType.Document,
                changeVector: null)))
            {
                logger.LogTrace("Opening the StreamReader...");
                using (var reader = new StreamReader(markovFile.Stream))
                {
                    source.AddRange(reader.ReadAllLines());
                }
                logger.LogTrace("Done! Source finished...");
            }
            logger.LogTrace("Shuffling {items} item(s)", source.Count.ToString("N0"));
            source.Shuffle(random);
            logger.LogTrace("Shuffling complete");
            await LoadServiceAsync();
            RecurringJob.AddOrUpdate("markovSaveContent", () => SaveServiceAsync(), Cron.Hourly);
            await base.InitializeService();
        }

        public async Task LoadServiceAsync()
        {
            logger.LogInformation($"Start Service Load...");
            using (var session = Raven.GetOrAddDocumentStore("erector_markov").OpenAsyncSession())
            {
                var content = await session.Query<MarkovContent>().ToListAsync();
                foreach (var mc in content)
                {
                    logger.LogTrace("Loading {serverId}...", mc.ServerId);
                    var context = mc.Context ?? Enumerable.Empty<string>();
                    var msi = new MarkovServerInstance(mc.ServerId, context);
                    chains.AddOrUpdate(mc.ServerId, (f) => msi, (f, g) => g);
                }
            }
            foreach(var server in Client.Guilds)
            {
                logger.LogInformation("Testing Load for Guild {serverId}", server.Id);
                TryGetServerInstance(server.Id, out _);
            }
            logger.LogInformation($"Finish Service Load...");
        }

        public async Task SaveServiceAsync()
        {
            logger.LogInformation($"Start Service Save...");
            using (var session = Raven.GetOrAddDocumentStore("erector_markov").OpenAsyncSession())
            {
                foreach (var kvp in chains)
                {
                    logger.LogTrace("Saving {serverId}...", kvp.Key);
                    await session.StoreAsync(new MarkovContent
                    {
                        ServerId = kvp.Key,
                        Context = kvp.Value._historicalMessages
                    }, kvp.Key.ToString());
                }
                await session.SaveChangesAsync();
            }
            logger.LogInformation($"Finish Service Save...");
        }

        public bool TryGetServerInstance(ulong chainId, out MarkovServerInstance msi)
        {
            if (!chains.ContainsKey(chainId))
            {
                logger.LogWarning("Missing Chain for Guild {chainId}; creating...", chainId);
                chains.AddOrUpdate(chainId, (f) => new MarkovServerInstance(f, Enumerable.Empty<string>()), (f, g) => g);
            }
            return chains.TryGetValue(chainId, out msi);
        }

        internal override async Task<bool> CanRespondToMessage(SocketUserMessage message, ulong instanceId)
        {
            // may not be necessary to send this, but, it seems appropriate
            logger.LogTrace("Checking to see if the message contains the appropriate trigger word...");
            bool containsTriggerWord = DoesMessageContainProperWord(message, out _);
            logger.LogTrace("{containsTriggerWord}: {containsTriggerWord}", nameof(containsTriggerWord), containsTriggerWord);

            ulong botId = Client.CurrentUser.Id;
            logger.LogTrace("Looking to see if the bot was mentioned...");
            containsTriggerWord |= message.MentionedUsers.Any(s => s.Id == botId); // The bot was mentioned.
            logger.LogTrace("Looking to see if the message was responded to (via Discord's resposne/reply system)");
            if (!containsTriggerWord && (message?.Reference?.MessageId.IsSpecified ?? false))
            {
                // AFAIK there is no reason that it's not a message channel
                logger.LogTrace("Fetching channel...");
                IMessageChannel messageChannel = (IMessageChannel)Client.GetChannel(message.Reference.ChannelId);
                logger.LogTrace("Fetching message...");
                IMessage msg = await messageChannel.GetMessageAsync(message.Reference.MessageId.Value);
                logger.LogTrace("Got message, checking message author ({authorId} == {botId})", msg.Author.Id, botId);
                if (msg.Author.Id == botId) containsTriggerWord = true;
            }
            containsTriggerWord |= message.Channel is IDMChannel;
            var serverInstance = chains.GetOrAdd(
                instanceId,
                s =>
                {
                    logger.LogTrace("Creating new chain for {server}", s);
                    return new MarkovServerInstance(s, Enumerable.Empty<string>());
                });
            serverInstance.AddHistoricalMessage(GetProperHistoricalMessage(message));
            return containsTriggerWord;
        }

        internal override Task<(bool, string)> DoesContainTriggerWord(SocketUserMessage message, ulong instanceId)
        {
            var canResponse = DoesMessageContainProperWord(message, out _);
            return Task.FromResult(canResponse ? (canResponse, Raven.Configuration.MarkovTrigger) : (canResponse, ""));
        }

        internal override Task<string> GenerateResponse(string triggerWord, SocketUserMessage message, ulong instanceId)
        {
            logger.LogTrace("Searching for instance...");
            var serverInstance = chains.GetOrAdd(
                instanceId,
                s =>
                {
                    logger.LogTrace("Creating new chain for {server}", s);
                    return new MarkovServerInstance(s, Enumerable.Empty<string>());
                });
            string messageToSend = null;
            logger.LogTrace("Using Instance: {serverId}", serverInstance.ServerId);
            logger.LogTrace("Acquiring exclusive lock on {serverId}", serverInstance.ServerId);
            lock (serverInstance)
            {
                // We need to generate a message in response since we were directly referenced.
                logger.LogTrace($"Generating a response...");
                var attempts = 0;
                while (string.IsNullOrWhiteSpace(messageToSend) && attempts++ <= 5)
                {
                    messageToSend = serverInstance.GetNextMessage();
                    logger.LogTrace("Response: '{messageToSend}'", messageToSend);
                }
                logger.LogTrace($"Response generated!");
            }
            logger.LogTrace($"Submitting Response...");
            return Task.FromResult(messageToSend);
        }


        private bool DoesMessageContainProperWord(SocketUserMessage message, out string cleanedInput)
        {
            var triggerWord = Raven.Configuration.MarkovTrigger;
            var containsTriggerWord = false;
            var messageFragments = message.CleanContent.Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList();
            for (var i = messageFragments.Count - 1; i >= 0; i--)
            {
                var insensitive = messageFragments[i].ToLower();
                if (insensitive.Equals(triggerWord, StringComparison.OrdinalIgnoreCase) ||
                    insensitive.IndexOf(triggerWord, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    logger.LogTrace($"Trigger word found in the message... remove it and get ready to send a new message");
                    containsTriggerWord = true;
                    messageFragments.RemoveAt(i);
                }
            }
            cleanedInput = string.Join(" ", messageFragments);
            return containsTriggerWord;
        }

        private string GetProperHistoricalMessage(SocketUserMessage message)
        {
            DoesMessageContainProperWord(message, out var clean);
            if (string.IsNullOrWhiteSpace(clean))
            {
                clean = message.Attachments?.FirstOrDefault()?.Url ?? "";
            }
            return clean;
        }

    }
}
