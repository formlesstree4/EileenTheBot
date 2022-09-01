using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bot.Models.BlackJack
{
    public sealed class BlackJackServerDetails
    {

        /// <summary>
        /// Gets or sets the Channel ID where 
        /// </summary>
        public ulong? ChannelId { get; set; } = null;

        /// <summary>
        /// Gets the collection of currently active games
        /// </summary>
        [JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public List<BlackJackTable> ActiveGames { get; set; } = new();

    }
}
