using System.Collections.Generic;

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
        public List<BlackJackTable> ActiveGames { get; set; } = new();

    }
}
