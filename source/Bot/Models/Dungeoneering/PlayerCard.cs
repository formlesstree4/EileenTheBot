using System.Collections.Generic;

namespace Bot.Models.Dungeoneering
{


    /// <summary>
    ///     Represents the player state in the Dungeoneering mini-game. Includes various things such as their equipment, encounters, wins and losses, and more!
    /// </summary>
    public sealed class PlayerCard
    {

        /// <summary>
        ///     Gets or sets the Player's race
        /// </summary>
        /// <remarks>
        ///     Generally means nothing
        /// </remarks>
        public Races Race { get; set; }

        /// <summary>
        ///     Gets or sets the number of victories for this Player
        /// </summary>
        public int Victories { get; set; }

        /// <summary>
        ///     Gets or sets the number of defeats for this Player
        /// </summary>
        public int Defeats { get; set; }

        /// <summary>
        ///     Gets or sets whether this Player Card is 'set in stone'.
        /// </summary>
        public bool IsConfirmed { get; set; }

        /// <summary>
        ///     Gets or sets the current gear the Player has equipped
        /// </summary>
        public IEnumerable<Equipment> Gear { get; set; }

        /// <summary>
        ///     Gets or sets the collection of fights this Player has been part of
        /// </summary>
        public IEnumerable<BattleLog> Battles { get; set; }

    }


}