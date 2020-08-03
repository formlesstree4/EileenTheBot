using System;
using System.Collections.Generic;
using Discord.WebSocket;

namespace Bot.Services.Markov
{
    public sealed class MarkovServerInstance
    {
        
        public ulong ServerId { get; }

        private readonly Stack<string> _historicalMessages;
        private readonly Random _random;
        private MarkovChain<string> _chain;

        public bool ReadyToMakeChain => _historicalMessages.Count >= 1000;


        public MarkovServerInstance(ulong serverId, IEnumerable<string> seed)
        {
            ServerId = serverId;
            _historicalMessages = new Stack<string>();
            _random = new SecureRandom();
            _chain = new MarkovChain<string>(_random);
            foreach(var i in seed) _chain.Add(i.Split(" ", StringSplitOptions.RemoveEmptyEntries));
        }

        public void AddHistoricalMessage(string message)
        {
            _historicalMessages.Push(message);
        }

        public string GetNextMessage() => string.Join(" ", _chain.Walk(_random));

        public void CreateChain()
        {
            _chain = new MarkovChain<string>(_random);
            while(_historicalMessages.Count > 0)
            {
                _chain.Add(_historicalMessages.Pop().Split(" ", StringSplitOptions.RemoveEmptyEntries));
            }
        }

    }

}