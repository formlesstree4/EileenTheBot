using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Bot.Models.Dungeoneering
{

    /// <summary>
    ///     Stores 'historical' information about a particular battle that occurred
    /// </summary>
    [JsonObject(IsReference = true)]
    public sealed class BattleLog
    {

        /// <summary>
        ///     Gets or sets the Monster that was fought
        /// </summary>
        public Monster MonsterFought { get; set; }

        /// <summary>
        ///     Gets or sets the Player that fought
        /// </summary>
        /// <remarks>This is a snapshot of the Player AT THE TIME they fought the Monster</remarks>
        public PlayerCard Player { get; set; }

        /// <summary>
        ///     Gets or sets people that assisted the Player
        /// </summary>
        public IEnumerable<PlayerCard> Assistants { get; set; } = Enumerable.Empty<PlayerCard>();

        /// <summary>
        ///     Gets or sets people that assisted the Monster
        /// </summary>
        public IEnumerable<PlayerCard> Instigators { get; set; } = Enumerable.Empty<PlayerCard>();

        /// <summary>
        ///     Gets or sets what Discord Channel ID the battle occurred in
        /// </summary>
        /// <value></value>
        public ulong ChannelId { get; set; }

        /// <summary>
        ///     Gets or sets whether or not the Player was victorious
        /// </summary>
        public string Result { get; set; }

    }


}