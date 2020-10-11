using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot.Services.Markov;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace Bot.Services
{
    public sealed class MarkovService
    {

        private readonly string _triggerWord;
        private readonly DiscordSocketClient _discord;
        private ConcurrentDictionary<ulong, MarkovServerInstance> _chains;
        private readonly List<string> _source;
        private readonly Random _random;
        private readonly char _prefix = Environment.GetEnvironmentVariable("CommandPrefix")[0];
        private readonly ulong _validServerId = 167274926883995648;


        public MarkovService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _triggerWord = Environment.GetEnvironmentVariable("MarkovTrigger") ?? "erector";
            _source = new List<string>();
            _random = new SecureRandom();
            _chains = new ConcurrentDictionary<ulong, MarkovServerInstance>();
        }


        public async Task InitializeService()
        {

            // look for markov.txt. It's a huge seeded file

            // var seedSize = _random.Next(500, 1000);
            // var seedCount = 0;
            using (var reader = new StreamReader("markov.txt"))
            {
                _source.AddRange(reader.ReadAllParagraphs());
            }

            _source.Shuffle(_random);
            _discord.MessageReceived += HandleIncomingMessage;

        }

        private async Task HandleIncomingMessage(SocketMessage rawMessage)
        {

            // Pre-filter some jargon out of here
            if (!(rawMessage is SocketUserMessage message)) return;
            if (!(message.Channel is IGuildChannel gc)) return;
            if (message.Source != MessageSource.User) return;
            if (message.HasPrefix()) return;
            if (gc.GuildId == _validServerId) return;

            // Find the appropriate instance to add to the source with it.
            var serverInstance = _chains.GetOrAdd(gc.GuildId, s => new MarkovServerInstance(s, GetSeedContent()));
            string messageToSend = null;

            lock (serverInstance)
            {

                // Break apart the message into fragments to filter out the trigger word.
                // Add it to the historical message for that instance.
                var messageFragments = message.Content.Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList();
                var containsTriggerWord = false;
                for (var i = messageFragments.Count - 1; i >= 0; i--)
                {
                    var insensitive = messageFragments[i].ToLowerInvariant();
                    if (insensitive.Equals(_triggerWord, StringComparison.OrdinalIgnoreCase) || insensitive.IndexOf(_triggerWord, StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        containsTriggerWord = true;
                        messageFragments.RemoveAt(i);
                    }
                }
                serverInstance.AddHistoricalMessage(string.Join(" ", messageFragments));
                if (!containsTriggerWord) return;

                // We need to generate a message in response since we were directly referenced.
                while (string.IsNullOrWhiteSpace(messageToSend))
                {
                    messageToSend = serverInstance.GetNextMessage();
                }

            }

            if (!string.IsNullOrWhiteSpace(messageToSend))
            {
                using (message.Channel.EnterTypingState())
                {
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

        public static bool HasPrefix(this IUserMessage message)
        {
            var prefix = Environment.GetEnvironmentVariable("CommandPrefix")[0];
            var position = 0;
            return message.HasCharPrefix(prefix, ref position);
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
