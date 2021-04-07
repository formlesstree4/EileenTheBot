using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.Dungeoneering
{


    /// <summary>
    ///     Represents the player state in the Dungeoneering mini-game. Includes various things such as their equipment, encounters, wins and losses, and more!
    /// </summary>
    [JsonObject(IsReference = true)]
    public sealed class PlayerCard
    {

        /// <summary>
        ///     Gets or sets the Player's race
        /// </summary>
        /// <remarks>
        ///     Generally means nothing
        /// </remarks>
        public string Race { get; set; }

        /// <summary>
        ///     Gets or sets the number of victories for this Player
        /// </summary>
        public int Victories { get; set; }

        /// <summary>
        ///     Gets or sets a small description about the Player
        /// </summary>
        public string Description { get; set; }

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
        public List<Equipment> Gear { get; set; } = new List<Equipment>();

        /// <summary>
        ///     Gets or sets the collection of fights this Player has been part of
        /// </summary>
        public List<BattleLog> Battles { get; set; } = new List<BattleLog>();

        /// <summary>
        ///     Gets or sets the current attack power of the Player
        /// </summary>
        /// <remarks>AttackPower is an ever adjusting stat that increments by 1 every time the Player wins and, upon defeat, gets halved, rounded down and a minimum value of 1.</remarks>
        public int AttackPower { get; set; }

        /// <summary>
        ///     Gets the acceptable attack value of the Player based upon Gear and victories thus far
        /// </summary>
        /// <returns></returns>
        public int GetActualPower() => AttackPower + (Gear?.Sum(c => c.AttackPower) ?? 0);

    }


}