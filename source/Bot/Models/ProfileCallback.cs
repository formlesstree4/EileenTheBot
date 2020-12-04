using Discord;

namespace Bot.Models
{

    /// <summary>
    ///     The object type used in the callback method for building a profile page
    /// </summary>
    /// <param name="UserData">A reference to the current <see cref="EileenUserData"/></param>
    /// <param name="CurrentUser">The <see cref="IUser"/> from Discord</param>
    /// <param name="PageBuilder">A reference to a builder model for generating the page</param>
    public sealed record ProfileCallback(
        EileenUserData UserData,
        IUser CurrentUser,
        EmbedBuilder PageBuilder);

}