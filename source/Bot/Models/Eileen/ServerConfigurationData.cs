using System.Collections.Generic;

namespace Bot.Models.Eileen
{


    public sealed class ServerConfigurationData
    {
        public ulong ServerId { get; set; }

        public IList<ulong> TrustedUsers { get; set; }

        public bool Enabled { get; set; } = true;
    }


}
