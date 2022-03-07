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
using Discord.Commands;
using Discord.WebSocket;
using Hangfire;
using Raven.Client.Documents;
using Raven.Client.Documents.Attachments;
using Raven.Client.Documents.Operations.Attachments;

namespace Bot.Services
{

    [Summary("Provides simplified management of the Markov Chains across all Guilds, Groups, and Private Channels")]
    public sealed class MarkovService // : IEileenService
    {

        private readonly string _triggerWord;
        private readonly DiscordSocketClient _discord;
        private readonly RavenDatabaseService _rdbs;
        private readonly ServerConfigurationService _serverConfigurationService;
        private readonly ConcurrentDictionary<ulong, MarkovServerInstance> _chains;
        private readonly List<string> _source;
        private readonly Random _random;
        private readonly Func<LogMessage, Task> WriteLog;


        public MarkovService(
            DiscordSocketClient client,
            RavenDatabaseService ravenDatabaseService,
            ServerConfigurationService serverConfigurationService,
            Func<LogMessage, Task> logger,
            Random random)
        {
            WriteLog = logger ?? (message => Task.CompletedTask);
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _discord = client;
            _rdbs = ravenDatabaseService;
            _serverConfigurationService = serverConfigurationService;
            var configuration = _rdbs.Configuration;
            Write("Setting Configuration...");
            _triggerWord = configuration.MarkovTrigger;
            Write($"Trigger Word '{_triggerWord}'");
            _source = new List<string>();
            _chains = new ConcurrentDictionary<ulong, MarkovServerInstance>();
        }


        public async Task InitializeService()
        {
            Write("Querying the database for the markov source file...");
            using (var markovFile = await _rdbs.GetOrAddDocumentStore("erector_core").Operations.SendAsync(new GetAttachmentOperation(
                documentId: "configuration",
                name: "markov.txt",
                type: AttachmentType.Document,
                changeVector: null)))
            {
                Write("Opening the StreamReader...", LogSeverity.Verbose);
                using (var reader = new StreamReader(markovFile.Stream))
                {
                    _source.AddRange(reader.ReadAllLines());
                }
                Write("Done! Source finished...", LogSeverity.Verbose);
            }

            Write($"Shuffling {_source.Count:N0} item(s)", LogSeverity.Verbose);
            _source.Shuffle(_random);
            Write("Shuffling complete", LogSeverity.Verbose);
            await LoadServiceAsync();
            Write($"Service has finished initializing... attaching {nameof(HandleIncomingMessage)}...");
            _discord.MessageReceived += HandleIncomingMessage;
            Write($"Hooking up hangfire job...");
            RecurringJob.AddOrUpdate("markovSaveContent", () => SaveServiceAsync(), Cron.Hourly);
            Write($"Hangfire job setup...");
        }

        public async Task SaveServiceAsync()
        {
            Write($"Start Service Save...");
            using (var session = _rdbs.GetOrAddDocumentStore("erector_markov").OpenAsyncSession())
            {
                foreach (var kvp in _chains)
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

        public async Task LoadServiceAsync()
        {
            Write($"Start Service Load...");
            using (var session = _rdbs.GetOrAddDocumentStore("erector_markov").OpenAsyncSession())
            {
                _chains.Clear();
                var content = await session.Query<MarkovContent>().ToListAsync();
                foreach (var mc in content)
                {
                    Write($"Loading {mc.ServerId}...", LogSeverity.Verbose);
                    var msi = new MarkovServerInstance(mc.ServerId, GetSeedContent());
                    Write($"Inserting Historical Context", LogSeverity.Verbose);
                    foreach (var c in mc.Context)
                    {
                        msi.AddHistoricalMessage(c);
                    }
                    _chains.AddOrUpdate(mc.ServerId, (f) => msi, (f, g) => g);
                }
            }
            Write($"Finish Service Load...");
        }

        public IEnumerable<ulong> GetServerIds() => _chains.Keys;

        public bool TryGetServerInstance(ulong chainId, out MarkovServerInstance msi)
        {
            return _chains.TryGetValue(chainId, out msi);
        }

        private async Task HandleIncomingMessage(SocketMessage rawMessage)
        {
            var position = 0;
            var serverId = 0UL;
            var isPrivate = false;

            if (rawMessage is not SocketUserMessage message) return;
            if (message.Source != MessageSource.User) return;
            if (message.Channel is IGuildChannel gc)
            {
                serverId = gc.GuildId;
                var cfg = await _serverConfigurationService.GetOrCreateConfigurationAsync(gc.GuildId);
                if (cfg.ResponderType != Models.ServerConfigurationData.AutomatedResponseType.Markov) return;
                if (message.HasCharPrefix(cfg.CommandPrefix, ref position)) return;
            }
            if (message.Channel is ITextChannel tc && tc.IsNsfw)
            {
                // Ignore NSFW channels
                return;
            }
            if (message.Channel is IPrivateChannel pc)
            {
                isPrivate = true;
                serverId = pc.Id;
            }

            // Find the appropriate instance to add to the source with it.
            Write("Searching for instance...", LogSeverity.Verbose);
            var serverInstance = _chains.GetOrAdd(
                serverId,
                s =>
                {
                    Write($"Creating new chain for {s}", LogSeverity.Verbose);
                    return new MarkovServerInstance(s, GetSeedContent());
                });
            string messageToSend = null;
            Write($"Using Instance: {serverInstance.ServerId}", LogSeverity.Verbose);
            Write($"Acquiring exclusive lock on {serverInstance.ServerId}", LogSeverity.Verbose);


            // Break apart the message into fragments to filter out the trigger word.
            // Add it to the historical message for that instance.
            var messageFragments = message.Content.Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList();
            var containsTriggerWord = false;
            Write($"Looking for trigger word in the message...", LogSeverity.Verbose);
            for (var i = messageFragments.Count - 1; i >= 0; i--)
            {
                var insensitive = messageFragments[i].ToLowerInvariant();
                if (insensitive.Equals(_triggerWord, StringComparison.OrdinalIgnoreCase) || insensitive.IndexOf(_triggerWord, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    Write($"Trigger word found in the message... remove it and get ready to send a new message", LogSeverity.Verbose);
                    containsTriggerWord = true;
                    messageFragments.RemoveAt(i);
                }
            }

            ulong botId = _discord.CurrentUser.Id;
            containsTriggerWord |= message.MentionedUsers.Any(s => s.Id == botId); // The bot was mentionned.
            if (!containsTriggerWord && (message?.Reference?.MessageId.IsSpecified ?? false))
            {
                // AFAIK there is no reason that it's not a message channel
                IMessageChannel messageChannel = (IMessageChannel)_discord.GetChannel(message.Reference.ChannelId);
                IMessage msg = await messageChannel.GetMessageAsync(message.Reference.MessageId.Value);
                if (msg.Author.Id == botId) containsTriggerWord = true;
            }

            lock (serverInstance)
            {
                serverInstance.AddHistoricalMessage(string.Join(" ", messageFragments));
            }
            if (!containsTriggerWord && !isPrivate) return;

            using (message.Channel.EnterTypingState()) // Send typing before starting to generate the response.
            {
                lock (serverInstance)
                {
                    // We need to generate a message in response since we were directly referenced.
                    Write($"Generating a response...", LogSeverity.Verbose);
                    var attempts = 0;
                    while (string.IsNullOrWhiteSpace(messageToSend) && attempts++ <= 5)
                    {
                        messageToSend = serverInstance.GetNextMessage();
                        Write($"Response: '{messageToSend}'", LogSeverity.Verbose);
                    }
                    Write($"Response generated!", LogSeverity.Verbose);

                }

                if (!string.IsNullOrWhiteSpace(messageToSend))
                {
                    Write($"Submitting Response...", LogSeverity.Verbose);

                    await message.Channel.SendMessageAsync(messageToSend);
                }
            }
        }

        private IEnumerable<string> GetSeedContent()
        {
            var content = new List<string>();
            lock (_source)
            {
                _source.Shuffle(_random);
                for (var i = 0; i < _random.Next(100); i++)
                {
                    content.Add(_source[i]);
                }
            }
            return content;
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            WriteLog(new LogMessage(severity, nameof(MarkovService), message));
        }

    }

}
