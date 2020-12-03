using System.Collections.Generic;

namespace Bot.Models.Raven
{
    public sealed class BotConfiguration
    {
        
        public string DiscordToken { get; set; }

        public IList<Bot.Models.CredentialsEntry> Credentials { get; set; }

        public char CommandPrefix { get; set; }

        public string MarkovTrigger { get; set; }

        public string GptUrl { get; set; }

        public int History { get; set; }

        public ConnectionString RelationalDatabase { get; set; }

        public ulong[] TrustedUsers { get; set; }

    }


    public sealed class ConnectionString
    {

        public string Username { get; set; }

        public string Password { get; set; }

        public string Hostname { get; set; }

        public string Database { get; set; }

    }

}