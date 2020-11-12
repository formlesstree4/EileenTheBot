using System.Collections.Generic;

namespace Bot.Models.Raven
{
    public sealed class BotConfiguration
    {
        

        public string DiscordToken { get; set; }

        public IList<Bot.Models.CredentialsEntry> Credentials { get; set; }

        public string CommandPrefix { get; set; }

        public string MarkovTrigger { get; set; }

        public string GptUrl { get; set; }

        public int History { get; set; }


    }
}