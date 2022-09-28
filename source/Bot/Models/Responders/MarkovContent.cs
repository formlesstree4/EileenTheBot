using System.Collections.Generic;

namespace Bot.Models.Responders
{

    public sealed class MarkovContent
    {

        public ulong ServerId { get; set; }

        public Queue<string> Context { get; set; }

    }


}
