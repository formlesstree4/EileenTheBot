using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Services.Communication
{
    public abstract class MessageResponderBase : IEileenService
    {
        private readonly DiscordSocketClient discordSocketClient;
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly Func<LogMessage, Task> logger;



        /// <summary>
        /// Gets whether or not this service will response to PMs
        /// </summary>
        /// <value></value>
        internal abstract bool CanRespondViaPM { get; }

        /// <summary>
        /// Gets whether or not this service will respond in NSFW channels
        /// </summary>
        /// <value></value>
        internal abstract bool CanRespondInNsfw { get; }

        /// <summary>
        /// Gets the client used for message communication
        /// </summary>
        internal DiscordSocketClient Client => discordSocketClient;

        /// <summary>
        /// Gets the connection for RavenDB
        /// </summary>
        internal RavenDatabaseService Raven => ravenDatabaseService;

        /// <summary>
        /// Gets the configuration service for services
        /// </summary>
        internal ServerConfigurationService ServerConfigurationService => serverConfigurationService;



        public MessageResponderBase(
            DiscordSocketClient discordSocketClient,
            RavenDatabaseService ravenDatabaseService,
            ServerConfigurationService serverConfigurationService,
            Func<LogMessage, Task> logger
        )
        {
            this.discordSocketClient = discordSocketClient;
            this.ravenDatabaseService = ravenDatabaseService;
            this.serverConfigurationService = serverConfigurationService;
            this.logger = logger;
        }

        public virtual Task InitializeService()
        {
            discordSocketClient.MessageReceived += HandleIncomingMessage;
            return Task.CompletedTask;
        }

        internal virtual async Task HandleIncomingMessage(SocketMessage rawMessage)
        {
            var instanceId = 0UL;

            if (rawMessage is not SocketUserMessage message) return;
            if (message.Source != MessageSource.User) return;
            var canRespondToMessage = await CanRespondToMessage(message);
            
            if (!canRespondToMessage)
            {
                return;
            }

            // filter out commands and
            // handle private messages
            switch (message.Channel)
            {
                case IGuildChannel gc:
                    var position = 0;
                    var cfg = await serverConfigurationService.GetOrCreateConfigurationAsync(gc.GuildId);
                    if (message.HasCharPrefix(cfg.CommandPrefix, ref position)) return;

                    // also handle ITextChannel here since
                    // ITextChannel implements IGuildChannel...
                    if (gc is ITextChannel tc && tc.IsNsfw && !CanRespondInNsfw)
                    {
                        return;
                    }
                    instanceId = gc.GuildId;

                    break;
                case IPrivateChannel pc:
                    if (!CanRespondViaPM) return;
                    instanceId = pc.Id;
                    break;
            }

            Write($"Looking for trigger word in the message...", severity: LogSeverity.Verbose);
            var shouldRespond = await DoesContainTriggerWord(message.Content, instanceId);

            if (shouldRespond.Item1 || canRespondToMessage)
            {
                var response = await GenerateResponse(shouldRespond.Item2, message.Content, instanceId);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    await message.ReplyAsync(response);
                }
            }

        }

        internal abstract Task<bool> CanRespondToMessage(SocketUserMessage message);
        internal abstract Task<(bool, string)> DoesContainTriggerWord(string message, ulong instanceId);
        internal abstract Task<string> GenerateResponse(string triggerWord, string message, ulong instanceId);


        internal virtual void Write(
            string message,
            string source = nameof(MessageResponderBase),
            LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, source, message));
        }

    }

}
