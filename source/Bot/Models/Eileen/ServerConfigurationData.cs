namespace Bot.Models.Eileen
{


    public sealed class ServerConfigurationData : TagEntry
    {

        public ulong ServerId { get; set; }

        public char CommandPrefix { get; set; }

        public AutomatedResponseType ResponderType { get; set; } = AutomatedResponseType.Markov;


        public enum AutomatedResponseType
        {
            GPT = 0,
            Markov = 1
        }

    }


}
