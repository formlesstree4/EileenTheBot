using Bot.Models.Eileen;
using Discord;

namespace Bot.Models.Casino.BlackJack
{
    /// <summary>
    /// Defines a Player that is sitting at a <see cref="BlackJackTable"/>
    /// </summary>
    public sealed class BlackJackPlayer : CasinoPlayer<BlackJackHand>
    {

        /// <summary>
        /// Gets if this Player came about as the result of a hand being split
        /// </summary>
        /// <remarks>
        /// This is used for eventually cleaning up excess players from the list of active players.
        /// </remarks>
        public bool IsFromSplit { get; private set; } = false;

        /// <summary>
        /// Creates a new Player object
        /// </summary>
        /// <param name="user">A reference to the <see cref="EileenUserData"/></param>
        /// <param name="discordUser">A reference to the <see cref="IUser"/></param>
        public BlackJackPlayer(EileenUserData user, IUser discordUser) :
            base(user, discordUser) { }

        /// <summary>
        /// Creates a cloned <see cref="BlackJackPlayer"/> that is set up to indicate it's from a split hand
        /// </summary>
        /// <param name="player">A reference to the <see cref="BlackJackPlayer"/></param>
        /// <param name="splitHand">The hand to set</param>
        /// <returns><see cref="BlackJackPlayer"/></returns>
        public static BlackJackPlayer CreateSplit(BlackJackPlayer player, BlackJackHand splitHand)
        {
            return new BlackJackPlayer(player.User, player.DiscordUser)
            {
                Hand = splitHand,
                IsFromSplit = true,
                CurrentBet = player.CurrentBet
            };
        }

        /// <inheritdoc cref="System.Object.ToString"/>
        public override string ToString() => Name;

    }
}
