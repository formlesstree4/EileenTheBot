using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Models;
using Bot.Services.Markov;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;

namespace Bot.Services
{
    public sealed class MarkovService
    {

        private readonly string _triggerWord;
        private readonly DiscordSocketClient _discord;
        private readonly RavenDatabaseService _rdbs;
        private ConcurrentDictionary<ulong, MarkovServerInstance> _chains;
        private readonly List<string> _source;
        private readonly Random _random;
        private readonly char _prefix;
        private readonly ulong _validServerId = 167274926883995648;


        public MarkovService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _rdbs = services.GetRequiredService<RavenDatabaseService>();
            var configuration = _rdbs.Configuration;
            _prefix = configuration.CommandPrefix[0];

            _triggerWord = configuration.MarkovTrigger;
            _source = new List<string>();
            _random = new SecureRandom();
            _chains = new ConcurrentDictionary<ulong, MarkovServerInstance>();
        }


        public async Task InitializeService()
        {

            // look for markov.txt. It's a huge seeded file
            using (var reader = new StreamReader("markov.txt"))
            {
                _source.AddRange(reader.ReadAllLines());
            }

            _source.Shuffle(_random);
            await LoadServiceAsync();
            _discord.MessageReceived += HandleIncomingMessage;
        }

        public async Task SaveServiceAsync()
        {
            using(var session = _rdbs.GetMarkovConnection.OpenAsyncSession())
            {
                foreach (var kvp in _chains)
                {
                    await session.StoreAsync(new MarkovContent
                    {
                        ServerId = kvp.Key,
                        Context = kvp.Value._historicalMessages,
                        CurrentChain = kvp.Value._chain
                    }, kvp.Key.ToString());
                }
                await session.SaveChangesAsync();
            }
        }

        public async Task LoadServiceAsync()
        {
            using (var session = _rdbs.GetMarkovConnection.OpenAsyncSession())
            {
                var content = await session.Query<MarkovContent>().ToListAsync();
                _chains.Clear();
                foreach(var mc in content)
                {
                    var msi = new MarkovServerInstance(mc.ServerId, mc.Context);
                    msi._chain = mc.CurrentChain;
                    _chains.AddOrUpdate(mc.ServerId, (f) => msi, (f, g) => g);
                }
            }
        }

        public void SaveService()
        {
            using(var session = _rdbs.GetMarkovConnection.OpenSession())
            {
                foreach (var kvp in _chains)
                {
                    session.Store(new MarkovContent
                    {
                        ServerId = kvp.Key,
                        Context = kvp.Value._historicalMessages,
                        CurrentChain = kvp.Value._chain
                    }, kvp.Key.ToString());
                }
                session.SaveChanges();
            }
        }

        private async Task HandleIncomingMessage(SocketMessage rawMessage)
        {
            var position = 0;
            // Pre-filter some jargon out of here
            if (!(rawMessage is SocketUserMessage message)) return;
            if (!(message.Channel is IGuildChannel gc)) return;
            if (message.Source != MessageSource.User) return;
            if (message.HasCharPrefix(_prefix, ref position)) return;
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
