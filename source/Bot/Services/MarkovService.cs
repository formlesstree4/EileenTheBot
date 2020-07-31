using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<ulong, SecureRandom> _randoms;
        private readonly ConcurrentDictionary<ulong, MarkovChain<string>> _chain;
        private readonly MarkovChain<string> _sourceChain;
        private readonly SecureRandom _sourceRandom;

        public MarkovService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _randoms = new ConcurrentDictionary<ulong, SecureRandom>();
            _chain = new ConcurrentDictionary<ulong, MarkovChain<string>>();
            _triggerWord = Environment.GetEnvironmentVariable("MarkovTrigger") ?? "erector";
            _discord.MessageReceived += HandleIncomingMessage;
            _sourceRandom = new SecureRandom();
            _sourceChain = new MarkovChain<string>(4, _sourceRandom);
        }


        public async Task InitializeFirstChain()
        {

            // look for markov.txt. It's a huge seeded file
            var seedSize = _sourceRandom.Next(10, 100);
            var seedCount = 0;
            using (var reader = new StreamReader("markov.txt"))
            {
                while (++seedCount <= seedSize)
                {
                    var nextLine = await reader.ReadLineAsync();
                    Console.WriteLine($"\tSeeding with: {nextLine}");
                    _sourceChain.Add(new[] { nextLine });
                }
            }

        }

        private async Task HandleIncomingMessage(SocketMessage rawMessage)
        {

            // Pre-filter some jargon out of here
            if (!(rawMessage is SocketUserMessage message)) return;
            if (!(message.Channel is IGuildChannel gc)) return;
            if (message.Source != MessageSource.User) return;

            // Get out the chain.
            var guildId = gc.Id;
            var rng = _randoms.GetOrAdd(guildId, _ => new SecureRandom());
            var mkc = _chain.GetOrAdd(guildId, _ =>
            {

                // for a new chain, we need to seed it
                var chain = new MarkovChain<string>(4, rng);
                var seed = _sourceChain.Walk(rng).SelectMany(c => c.Split(" ", StringSplitOptions.RemoveEmptyEntries));
                chain.Add(seed);

                Console.WriteLine($"Creating Chain for Guild {guildId}");
                return chain;

            });

            Console.WriteLine($"Adding {message.Content} to the chain...");

            // Add our new data to it
            mkc.Add(message.Content.Split(" ", StringSplitOptions.RemoveEmptyEntries));

            // If the message contains "erector", have it respond.
            if (message.Content.IndexOf("erector", StringComparison.OrdinalIgnoreCase) == -1) return;

            // Now, let's push out the message back to the appropriate channel
            var messageToSend = string.Join(string.Empty, mkc.Walk());

            using (message.Channel.EnterTypingState())
            {
                await message.Channel.SendMessageAsync(messageToSend);
            }

        }



    }
}