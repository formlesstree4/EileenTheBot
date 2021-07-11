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
                    await message.Channel.SendMessageAsync(response);
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

    internal static class Extensions
    {

        public static void Shuffle<T>(this IList<T> list, Random random)
        {

            var n = list.Count;
            for (var i = 0; i < n; i++)
            {
                var r = i + (int)(random.NextDouble() * (n - i));
                var t = list[r];
                list[r] = list[i];
                list[i] = t;
            }

        }

        public static IEnumerable<string> ReadAllLines(this StreamReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        public static string ReadParagraph(this StreamReader reader)
        {

            // so we'll loop until an empty line shows up
            var builder = new System.Text.StringBuilder();
            while (true)
            {
                var currentLine = reader.ReadLine();
                if (currentLine == null) break; // no more shit to read

                // If the current line is empty and we have NOTHING saved in the builder
                // then advance the reader and don't worry about it for now
                if (string.IsNullOrWhiteSpace(currentLine) && builder.Length == 0) continue;

                // If the current line is empty and there's something in the builder
                // return out the string as the 'end of paragraph'.
                if (string.IsNullOrWhiteSpace(currentLine)) break;

                // Append to the builder, prefixing a space to the line
                builder.Append(" ").Append(currentLine);
            }

            return builder.ToString();

        }

        public static IEnumerable<string> ReadAllParagraphs(this StreamReader reader)
        {
            while (reader.Peek() >= 0)
            {
                yield return reader.ReadParagraph();
            }
        }

        public static string Extract(this string content, string start, string end,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase, int instance = 1)
        {
            var startIndex = content.Seek(start, comparison, instance);
            var endIndex = content.Seek(end, comparison, start.Equals(end, comparison) ? instance + 1 : instance);
            startIndex += start.Length;
            return content.Extract(startIndex, endIndex);
        }

        public static string Extract(this string content, int start, int end)
        {
            return content.Substring(start, end - start);
        }

        public static int Seek(this string content, string item, StringComparison comparison = StringComparison.OrdinalIgnoreCase, int instance = 1)
        {
            var location = 0;
            for (var instanceCounter = 0; instanceCounter < instance; instanceCounter++)
            {
                if (location == -1) break;
                location = content.IndexOf(item, instanceCounter == 0 ? location : location + 1, comparison);
            }
            return location;
        }
    }


}