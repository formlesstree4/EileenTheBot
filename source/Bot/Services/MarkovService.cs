using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot.Services.Markov;
using Discord;
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
        private bool _isReady = false;

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
            var seedSize = _random.Next(50, 100);
            var seedCount = 0;
            using (var reader = new StreamReader("markov.txt"))
            {
                while (++seedCount <= seedSize && !reader.EndOfStream)
                {
                    var nextLine = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(nextLine))
                    {
                        _source.Add(nextLine);
                    }
                }
            }

            _source.Shuffle(_random);
            _discord.MessageReceived += HandleIncomingMessage;
            _isReady = true;

        }

        private async Task HandleIncomingMessage(SocketMessage rawMessage)
        {

            // Pre-filter some jargon out of here
            if (!(rawMessage is SocketUserMessage message)) return;
            if (!(message.Channel is IGuildChannel gc)) return;
            if (message.Source != MessageSource.User) return;

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


    }

}
