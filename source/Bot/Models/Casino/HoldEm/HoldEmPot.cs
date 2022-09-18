using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Bot.Models.Casino.HoldEm
{
    public sealed class HoldEmPot
    {

        /// <summary>
        /// Gets a collection of players that are vested in this <see cref="HoldEmPot"/> alongside how much they've bet
        /// </summary>
        public IReadOnlyDictionary<HoldEmPlayer, ulong> VestedPlayers { get; }

        /// <summary>
        /// Gets the name of the pot
        /// </summary>
        public string Name { get; } = "";

        /// <summary>
        /// Gets the current value of the pot
        /// </summary>
        public ulong Value { get; set; } = 0;

        /// <summary>
        /// Gets the maximum value for this pot
        /// </summary>
        public ulong MaxValue { get; } = 0;

        /// <summary>
        /// Gets the maximum value that an individual player (inside <see cref="VestedPlayers"/>) can put in the pot
        /// </summary>
        public ulong MaxIndividualValue { get; } = 0;



        /// <summary>
        /// Creates a new Pot for Hold 'Em
        /// </summary>
        /// <param name="players">The players that care about this pot</param>
        public HoldEmPot(string name, IEnumerable<HoldEmPlayer> players)
        {
            Name = name;
            VestedPlayers = new ReadOnlyDictionary<HoldEmPlayer, ulong>(players.ToDictionary(p => p, p => 0UL));
            MaxIndividualValue = players.Min(c => c.CurrencyData.Currency);
            MaxValue = MaxIndividualValue * (ulong)VestedPlayers.Count;
        }

    }
}
