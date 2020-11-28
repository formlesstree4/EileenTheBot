using System.Collections.Generic;
using Discord;

namespace Bot.Models.CommandPermissions
{

    /// <summary>
    ///     Describes what channels a Command has potentially non-default behavior in.
    /// </summary>
    public sealed class CommandChannelDetails
    {

        /// <summary>
        ///     Gets or sets a collection of Channel IDs that this command is allowed to run in.
        /// </summary>
        /// <value></value>
        public List<ulong> Allowed { get; set; }

        /// <summary>
        ///     Gets or sets a collection of Channel IDs that this command is NOT allwed to run in.
        /// </summary>
        /// <value></value>
        public List<ulong> Blocked { get; set; }



        /// <summary>
        ///     Checks to see if the given channel has been explicitly blocked from having this command execute.
        /// </summary>
        /// <param name="channel"><see cref="IChannel"/></param>
        /// <returns>boolean</returns>
        public bool IsChannelBlocked(IChannel channel) => Blocked?.Contains(channel.Id) ?? false;

        /// <summary>
        ///     Checks to see if the given channel has been explicitly allowed to have this command execute.
        /// </summary>
        /// <param name="channel"><see cref="IChannel"/></param>
        /// <returns>boolean</returns>
        public bool IsChannelAllowed(IChannel channel) => Allowed?.Contains(channel.Id) ?? false;

    }

}