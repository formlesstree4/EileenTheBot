namespace Bot.Models
{


    public sealed class ServerConfigurationData : TagEntry
    {

        public ulong ServerId { get; set; }

        public char CommandPrefix { get; set; }

    }


}