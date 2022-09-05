using Bot.Models.Casino;
using Discord;

namespace Bot.Models.Casino.BlackJack
{
    /// <summary>
    ///     Defines a Player that is sitting at a <see cref="BlackJackTable"/>
    /// </summary>
    public sealed class BlackJackPlayer
    {

        /// <summary>
        ///     Gets the current User
        /// </summary>
        public EileenUserData User { get; }

        /// <summary>
        ///     Gets the <see cref="IUser"/> associated with this Player
        /// </summary>
        public IUser DiscordUser { get; }

        /// <summary>
        /// Gets the name of the Player
        /// </summary>
        public string Name => DiscordUser?.Username ?? "Dealer";

        /// <summary>
        ///     Gets the User's hand
        /// </summary>
        public Hand Hand { get; private set; } = new();

        /// <summary>
        ///     Indicates if this is the Dealer or not
        /// </summary>
        public bool IsDealer => User == null;

        /// <summary>
        ///     Gets if this Player came about as the result of a hand being split
        /// </summary>
        /// <remarks>
        ///     This is used for eventually cleaning up excess players from the list of active players.
        /// </remarks>
        public bool IsFromSplit { get; private set; } = false;

        /// <summary>
        ///     Gets or sets the current Bet for this player
        /// </summary>
        public ulong CurrentBet { get; set; }



        /// <summary>
        ///     Creates a new Player object
        /// </summary>
        /// <param name="user">A reference to the <see cref="EileenUserData"/></param>
        /// <param name="discordUser">A reference to the <see cref="IUser"/></param>
        public BlackJackPlayer(EileenUserData user, IUser discordUser)
        {
            User = user;
            DiscordUser = discordUser;
        }

        /// <summary>
        ///     Creates a cloned <see cref="BlackJackPlayer"/> that is setup to indicate its from a split hand
        /// </summary>
        /// <param name="user">A reference to the <see cref="EileenUserData"/></param>
        /// <param name="discordUser">A reference to the <see cref="IUser"/></param>
        /// <param name="splitHand">The hand to set</param>
        /// <returns><see cref="BlackJackPlayer"/></returns>
        public static BlackJackPlayer CreateSplit(BlackJackPlayer player, Hand splitHand)
        {
            return new BlackJackPlayer(player.User, player.DiscordUser)
            {
                Hand = splitHand,
                IsFromSplit = true,
                CurrentBet = player.CurrentBet
            };
        }


        public override string ToString() => Name;

    }
}
