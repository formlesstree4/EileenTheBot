using System.Collections.Generic;

namespace Bot.Models.Dungeoneering
{

    /// <summary>
    ///     Defines an encounter setup in a channel
    /// </summary>
    public sealed class Encounter
    {

        /// <summary>
        ///     Gets or sets the channel ID where this encounter is active at.
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        ///     Gets or sets the monster for this encounter.
        /// </summary>
        public Monster ActiveMonster { get; set; }

        /// <summary>
        ///     Gets or sets the loot for this encounter.
        /// </summary>
        public List<Item> Loot { get; set; }

        /// <summary>
        ///     Gets or sets the Player ID that initiated this encounter.
        /// </summary>
        public ulong PlayerId { get; set; }

        /// <summary>
        /// Gets or sets a list of instigators
        /// </summary>
        /// <typeparam name="ulong">Discord assigned snowflake IDs</typeparam>
        /// <returns></returns>
        public List<ulong> Instigators { get; set; } = new List<ulong>();

        /// <summary>
        /// Gets or sets a list of assistants
        /// </summary>
        /// <typeparam name="ulong"></typeparam>
        /// <returns></returns>
        public List<ulong> Assistants { get; set; } = new List<ulong>();

    }


}