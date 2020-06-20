using System;

namespace Bot.Models
{
    public sealed class Player
    {

        /// <summary>Gets or sets the Discord ID associated with the Player</summary>
        public long PlayerId { get; set; }

        /// <summary>Gets or sets the Discord ID associated with the Server</summary>
        public long ServerId { get; set; }

        /// <summary>Gets or sets the amount of CURRENCY the Player has</summary>
        public decimal Currency { get; set; }

    }
}

