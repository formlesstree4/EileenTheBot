using Discord;

namespace Bot.Models.Casino
{

    /// <summary>
    ///     The base player object that all Casino games have in common
    /// </summary>
    public abstract class CasinoPlayer
    {

        /// <summary>
        ///     Gets the current User
        /// </summary>
        public EileenUserData User { get; private set; }

        /// <summary>
        ///     Gets the <see cref="IUser"/> associated with this Player
        /// </summary>
        public IUser DiscordUser { get; private set; }

        /// <summary>
        /// Gets the name of the Player
        /// </summary>
        public string Name => DiscordUser?.Username ?? "Dealer";

        /// <summary>
        ///     Gets the User's hand
        /// </summary>
        public Hand Hand { get; internal set; } = new();

        /// <summary>
        ///     Indicates if this is the Dealer or not
        /// </summary>
        public bool IsDealer => User == null;

        /// <summary>
        ///     Gets or sets the current Bet for this player
        /// </summary>
        public ulong CurrentBet { get; set; }



        /// <summary>
        /// Creates a new <see cref="CasinoPlayer"/>
        /// </summary>
        /// <param name="userData"><see cref="EileenUserData"/></param>
        /// <param name="user"><see cref="IUser"/></param>
        public CasinoPlayer(EileenUserData userData, IUser user)
        {
            User = userData;
            DiscordUser = user;
        }


    }
}
