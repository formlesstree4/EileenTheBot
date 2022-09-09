using System.Linq;

namespace Bot.Models.Casino.HoldEm
{

    /// <summary>
    ///     Represents a Texas Hold'Em table
    /// </summary>
    public sealed class HoldEmTable : CasinoTable<HoldEmPlayer>
    {

        /// <summary>
        /// Creates a new <see cref="HoldEmTable"/> with a default deck
        /// </summary>
        public HoldEmTable() :
            this(new Deck(Deck.StandardDeck))
        { }

        /// <summary>
        /// Creats a new <see cref="HoldEmTable"/> with the specified deck
        /// </summary>
        /// <param name="deck">The <see cref="Deck"/> to use</param>
        public HoldEmTable(Deck deck) :
            base(new(null, null, int.MinValue), deck)
        { }

        /// <summary>
        /// Sorts the players in their proper turn order for Hold'Em
        /// </summary>
        /// <remarks>
        ///     The first player to go should be the player right after the big blind
        ///     The third from last player to go should be the Dealer button
        ///     The second from last player to go should be the small blind
        ///     The last player to go should be the big blind
        /// </remarks>
        public override void PopulateTableStack()
        {
            foreach(var player in Players.OrderByDescending(p => p.GetSortNumber()))
            {
                CurrentRoundPlayers.Push(player);
            }
        }

    }

}
