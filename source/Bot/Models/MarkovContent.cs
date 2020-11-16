using System.Collections.Generic;
using Bot.Services.Markov;

namespace Bot.Models
{

    public sealed class MarkovContent
    {

        public ulong ServerId { get; set; }

        public Queue<string> Context { get; set; }

        public MarkovChain<string> CurrentChain { get; set; }

    }


}