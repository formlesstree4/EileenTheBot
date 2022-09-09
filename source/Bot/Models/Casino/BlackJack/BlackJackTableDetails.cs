using Discord;

namespace Bot.Models.Casino.BlackJack
{
    public sealed class BlackJackTableDetails : CasinoTableDetails<BlackJackTable, BlackJackPlayer>
    {
        public BlackJackTableDetails(BlackJackTable table, IThreadChannel threadChannel) : base(table, threadChannel) { }
    }
}
