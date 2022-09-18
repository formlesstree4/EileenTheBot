namespace Bot.Models.Casino.BlackJack
{

    /// <summary>
    /// Defines BlackJack specific properties for the <see cref="CasinoHand"/> class
    /// </summary>
    public sealed class BlackJackHand : CasinoHand
    {

        /// <summary>
        ///     Gets whether or not this hand is a bust.
        /// </summary>
        public bool IsBust
        {
            get
            {
                return Value > 21;
            }
        }

        /// <summary>
        ///     Gets whether this hand is blackjack or not.
        /// </summary>
        public bool IsBlackJack
        {
            get
            {
                return Cards.Count == 2 && Value == 21;
            }
        }

        /// <summary>
        ///     Gets whether this Hand can be split into two hands.
        /// </summary>
        public bool IsSplittable
        {
            get
            {
                return Cards.Count == 2 && Cards[0].Face == Cards[1].Face;
            }
        }

        /// <summary>
        /// Gets or sets whether this was from a split
        /// </summary>
        public bool IsFromSplit { get; } = false;

    }
}
