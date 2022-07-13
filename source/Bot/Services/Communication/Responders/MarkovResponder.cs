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
        internal override bool CanRespondInNsfw => true;


        public MarkovResponder(
            DiscordSocketClient discordSocketClient,
            RavenDatabaseService ravenDatabaseService,
            ServerConfigurationService serverConfigurationService,
            Func<LogMessage, Task> logger,
            Random random) : base(discordSocketClient, ravenDatabaseService, serverConfigurationService, logger)
        {
            chains = new ConcurrentDictionary<ulong, MarkovServerInstance>();
            source = new List<string>();
            this.random = random;
        }


        public override async Task InitializeService()
        {
            Write("Querying the database for the markov source file...");
            using (var markovFile = await Raven.GetOrAddDocumentStore("erector_core").Operations.SendAsync(new GetAttachmentOperation(
                documentId: "configuration",
                name: "markov.txt",
                type: AttachmentType.Document,
                changeVector: null)))
            {
                Write("Opening the StreamReader...", severity: LogSeverity.Verbose);
                using (var reader = new StreamReader(markovFile.Stream))
                {
                    source.AddRange(reader.ReadAllLines());
                }
                Write("Done! Source finished...", LogSeverity.Verbose);
            }
            Write($"Shuffling {source.Count:N0} item(s)", LogSeverity.Verbose);
            source.Shuffle(random);
            Write("Shuffling complete", LogSeverity.Verbose);
            await LoadServiceAsync();
            RecurringJob.AddOrUpdate("markovSaveContent", () => SaveServiceAsync(), Cron.Hourly);
            await base.InitializeService();
        }

        public async Task LoadServiceAsync()
        {
            Write($"Start Service Load...");
            using (var session = Raven.GetOrAddDocumentStore("erector_markov").OpenAsyncSession())
            {
                chains.Clear();
                var content = await session.Query<MarkovContent>().ToListAsync();
                foreach (var mc in content)
                {
                    Write($"Loading {mc.ServerId}...", LogSeverity.Verbose);
                    var context = mc.Context ?? Enumerable.Empty<string>();
                    var msi = new MarkovServerInstance(mc.ServerId, context);
                    chains.AddOrUpdate(mc.ServerId, (f) => msi, (f, g) => g);
                }
            }
            foreach(var server in Client.Guilds)
            {
                Write($"Testing Load for Guild {server.Id}");
                TryGetServerInstance(server.Id, out _);
            }
            Write($"Finish Service Load...");
        }

        public async Task SaveServiceAsync()
        {
            Write($"Start Service Save...");
            using (var session = Raven.GetOrAddDocumentStore("erector_markov").OpenAsyncSession())
            {
                foreach (var kvp in chains)
                {
                    Write($"Saving {kvp.Key}...", LogSeverity.Verbose);
                    await session.StoreAsync(new MarkovContent
                    {
                        ServerId = kvp.Key,
                        Context = kvp.Value._historicalMessages
                    }, kvp.Key.ToString());
                }
                await session.SaveChangesAsync();
            }
            Write($"Finish Service Save...");
        }

        public bool TryGetServerInstance(ulong chainId, out MarkovServerInstance msi)
        {
            if (!chains.ContainsKey(chainId))
            {
                Write($"Missing Chain for Guild {chainId}");
                chains.AddOrUpdate(chainId, (f) => new MarkovServerInstance(f, Enumerable.Empty<string>()), (f, g) => g);
            }
            return chains.TryGetValue(chainId, out msi);
        }

        internal override async Task<bool> CanRespondToMessage(SocketUserMessage message)
        {
            // may not be necessary to send this, but, it seems appropriate
            Write("Checking to see if the message contains the appropriate trigger word...", LogSeverity.Verbose);
            bool containsTriggerWord = DoesMessageContainProperWord(message, out _);
            Write($"{nameof(containsTriggerWord)}: {containsTriggerWord}", LogSeverity.Verbose);

            ulong botId = Client.CurrentUser.Id;
            Write("Looking to see if the bot was mentioned...", LogSeverity.Verbose);
            containsTriggerWord |= message.MentionedUsers.Any(s => s.Id == botId); // The bot was mentioned.
            Write($"{nameof(containsTriggerWord)}: {containsTriggerWord}", LogSeverity.Verbose);
            Write("Looking to see if the message was responded to (via Discord's reponse system)", LogSeverity.Verbose);
            if (!containsTriggerWord && (message?.Reference?.MessageId.IsSpecified ?? false))
            {
                // AFAIK there is no reason that it's not a message channel
                Write("Fetching channel...");
                IMessageChannel messageChannel = (IMessageChannel)Client.GetChannel(message.Reference.ChannelId);
                Write("Fetching message...");
                IMessage msg = await messageChannel.GetMessageAsync(message.Reference.MessageId.Value);
                Write($"Got message, checking message author ({msg.Author.Id} == {botId})", LogSeverity.Verbose);
                if (msg.Author.Id == botId) containsTriggerWord = true;
            }
            containsTriggerWord |= message.Channel is IDMChannel;
            Write($"{nameof(containsTriggerWord)}: {containsTriggerWord}", LogSeverity.Verbose);
            return containsTriggerWord;
        }

        internal override Task<(bool, string)> DoesContainTriggerWord(SocketUserMessage message, ulong instanceId)
        {
            var canResponse = DoesMessageContainProperWord(message, out _);
            return Task.FromResult(canResponse ? (canResponse, Raven.Configuration.MarkovTrigger) : (canResponse, ""));
        }

        internal override Task<string> GenerateResponse(string triggerWord, SocketUserMessage message, ulong instanceId)
        {
            Write("Searching for instance...", LogSeverity.Verbose);
            var serverInstance = chains.GetOrAdd(
                instanceId,
                s =>
                {
                    Write($"Creating new chain for {s}", LogSeverity.Verbose);
                    return new MarkovServerInstance(s, Enumerable.Empty<string>());
                });
            string messageToSend = null;
            Write($"Using Instance: {serverInstance.ServerId}", LogSeverity.Verbose);
            Write($"Acquiring exclusive lock on {serverInstance.ServerId}", LogSeverity.Verbose);
            lock (serverInstance)
            {
                // We need to generate a message in response since we were directly referenced.
                serverInstance.AddHistoricalMessage(GetProperHistoricalMessage(message));
                Write($"Generating a response...", LogSeverity.Verbose);
                var attempts = 0;
                while (string.IsNullOrWhiteSpace(messageToSend) && attempts++ <= 5)
                {
                    messageToSend = serverInstance.GetNextMessage();
                    Write($"Response: '{messageToSend}'", LogSeverity.Verbose);
                }
                Write($"Response generated!", LogSeverity.Verbose);
            }
            Write($"Submitting Response...", LogSeverity.Verbose);
            return Task.FromResult(messageToSend);
        }


        private bool DoesMessageContainProperWord(SocketUserMessage message, out string cleanedInput)
        {
            var triggerWord = Raven.Configuration.MarkovTrigger;
            var containsTriggerWord = false;
            var messageFragments = message.CleanContent.Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList();
            for (var i = messageFragments.Count - 1; i >= 0; i--)
            {
                var insensitive = messageFragments[i].ToLowerInvariant();
                if (insensitive.Equals(triggerWord, StringComparison.OrdinalIgnoreCase) ||
                    insensitive.IndexOf(triggerWord, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    Write($"Trigger word found in the message... remove it and get ready to send a new message", nameof(MarkovResponder), LogSeverity.Verbose);
                    containsTriggerWord = true;
                    messageFragments.RemoveAt(i);
                }
            }
            cleanedInput = string.Join(" ", messageFragments);
            return containsTriggerWord;
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            base.Write(message, nameof(MarkovResponder), severity);
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
