using System;
using System.Collections.Generic;
using Discord.WebSocket;

namespace Bot.Services.Markov
{
    public class MarkovServerInstance
    {
        
        public ulong ServerId { get; }

        private readonly Stack<string> _historicalMessages;
        private readonly DiscordSocketClient _client;
        private readonly MarkovChain<string> _chain;
        private readonly Random _random;

        public MarkovServerInstance(ulong serverId, DiscordSocketClient client)
        {
            ServerId = serverId;
            _client = client;
            _historicalMessages = new Stack<string>();
            _random = new SecureRandom();
            _chain = new MarkovChain<string>(_random);
        }

    }

}