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
        private readonly MarkovChain<string> _sourceChain;
        private readonly Stack<IEnumerable<string>> _sourceHistory;
        private readonly Random _sourceRandom;

        public MarkovService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _sourceRandom = new Random();
            _sourceChain = new MarkovChain<string>(1, _sourceRandom);
            _sourceHistory = new Stack<IEnumerable<string>>();
            _triggerWord = Environment.GetEnvironmentVariable("MarkovTrigger") ?? "erector";
            _discord.MessageReceived += HandleIncomingMessage;
        }


        public async Task InitializeFirstChain()
        {

            // look for markov.txt. It's a huge seeded file
            var seedSize = _sourceRandom.Next(50, 100);
            var seedCount = 0;
            using (var reader = new StreamReader("markov.txt"))
            {
                while (++seedCount <= seedSize)
                {
                    var nextLine = await reader.ReadLineAsync();
                    Console.WriteLine($"\tSeeding with: {nextLine}");
                    _sourceChain.Add(nextLine.Split(" ", StringSplitOptions.RemoveEmptyEntries));
                }
            }

        }

        private async Task HandleIncomingMessage(SocketMessage rawMessage)
        {

            // Pre-filter some jargon out of here
            if (!(rawMessage is SocketUserMessage message)) return;
            if (!(message.Channel is IGuildChannel gc)) return;
            if (message.Source != MessageSource.User) return;

            // Okay. Fuck it.
            Console.WriteLine($"Adding {message.Content} to the chain...");
            _sourceChain.Add(message.Content.Split(" ", StringSplitOptions.RemoveEmptyEntries));

            if (message.Content.IndexOf(_triggerWord, StringComparison.OrdinalIgnoreCase) == -1) return;
            Console.WriteLine($"Generating a new message");

            var generatedMessage = _sourceChain.Walk(
                _sourceHistory.Count > 0 ? _sourceHistory.Peek(): Enumerable.Empty<string>(), _sourceRandom);
            var messageToSend = string.Join(" ", generatedMessage);
            _sourceHistory.Push(generatedMessage);

            using (message.Channel.EnterTypingState())
            {
                await message.Channel.SendMessageAsync(messageToSend);
            }

            // Get out the chain.
            // var guildId = gc.GuildId;
            // var rng = _randoms.GetOrAdd(guildId, _ => new SecureRandom());
            // var mkc = _chain.GetOrAdd(guildId, _ =>
            // {
            //     var chain = new MarkovChain<string>(4, rng);
            //     var seed = _sourceChain.Walk(rng).SelectMany(c => c.Split(" ", StringSplitOptions.RemoveEmptyEntries));
            //     chain.Add(seed, rng.Next(1, 6));
            //     Console.WriteLine($"Creating Chain for Guild {guildId}");
            //     return chain;
            // });
            // var hst = _history.GetOrAdd(guildId, _ => new Stack<IEnumerable<string>>());

            

            // // Add our new data to it
            // mkc.Add(message.Content.Split(" ", StringSplitOptions.RemoveEmptyEntries), rng.Next(3, 10));

            // // If the message contains "erector", have it respond.
            // if (message.Content.IndexOf(_triggerWord, StringComparison.OrdinalIgnoreCase) == -1) return;

            // // Now, let's push out the message back to the appropriate channel
            // var generatedMessage = mkc.Walk(hst.Count > 0 ? hst.Peek(): Enumerable.Empty<string>());
            // hst.Push(generatedMessage);
            // var messageToSend = string.Join(" ", generatedMessage);

        }

    }

}
