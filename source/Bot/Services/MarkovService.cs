using System;
using System.Collections.Concurrent;
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

        public MarkovService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _randoms = new ConcurrentDictionary<ulong, SecureRandom>();
            _chain = new ConcurrentDictionary<ulong, MarkovChain<string>>();
            _triggerWord = Environment.GetEnvironmentVariable("MarkovTrigger") ?? "erector";
            _discord.MessageReceived += HandleIncomingMessage;
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
                var chain = new MarkovChain<string>(rng);
                var seed = _sourceChain.Walk(rng);
                chain.Add(seed);
                return chain;

            });

            // Add our new data to it
            mkc.Add(new[] { message.Content });

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