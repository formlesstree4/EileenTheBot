using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Services.Markov
{
    public sealed class MarkovServerInstance
    {
        private const int ChainRefreshCount = 100;
        private const int MaxHistoryCount = 1000;
        private bool refreshAsap = false;

        public ulong ServerId { get; }

        public Queue<string> _historicalMessages;
        public readonly Random _random;
        public MarkovChain<string> _chain;
        public IEnumerable<string> _seed;

        public bool ReadyToMakeChain => _historicalMessages.Count % ChainRefreshCount == 0;
        public bool ReadyToCleanHistory => _historicalMessages.Count > MaxHistoryCount;


        public MarkovServerInstance(ulong serverId, IEnumerable<string> seed)
        {
            ServerId = serverId;
            _historicalMessages = new Queue<string>();
            _random = new SecureRandom();
            _chain = new MarkovChain<string>(_random);
            _seed = seed;
            foreach (var i in _seed) _chain.Add(i.Split(" ", StringSplitOptions.RemoveEmptyEntries));
            if (!seed.Any())
            {
                refreshAsap = true;
            }
        }





        public void AddHistoricalMessage(string message)
        {
            _historicalMessages.Enqueue(message);
            if (refreshAsap || ReadyToMakeChain) CreateChain();
            if (ReadyToCleanHistory) CleanHistory();
        }

        public void RetrainFromScratch(IEnumerable<string> seed)
        {
            _historicalMessages = new Queue<string>(seed);
            _seed = seed;
            CreateChain();
        }

        public string GetNextMessage() => string.Join(" ", _chain.Walk(_random));

        private void CreateChain()
        {
            refreshAsap = false;
            _chain = new MarkovChain<string>(_random);
            foreach (var i in _historicalMessages)
            {
                _chain.Add(i.Split(" ", StringSplitOptions.RemoveEmptyEntries));
            }
            foreach (var i in _seed)
            {
                _chain.Add(i.Split(" ", StringSplitOptions.RemoveEmptyEntries));
            }
        }

        private void CleanHistory()
        {
            for (var i = 0; i < ChainRefreshCount; i++)
            {
                _historicalMessages.Dequeue();
            }
        }

    }

}
