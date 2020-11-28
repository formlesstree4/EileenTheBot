namespace Bot.Models.CommandPermissions
{

    /// <summary>
    ///     An entry in the permissions system for a particular command.
    /// </summary>
    public sealed class CommandEntry
    {

        /// <summary>
        ///     Gets or sets the name of the command this entry is for
        /// </summary>
        /// <value></value>
        public string Command { get; set; }

        /// <summary>
        ///     Gets or sets whether the command is enabled by default. A value of 'true' means enabled by default.
        /// </summary>
        /// <value></value>
        public bool Default { get; set; }

        /// <summary>
        ///     Gets or sets whether the command is allowed to run in a private channel. This field may eventually become deprecated.
        /// </summary>
        /// <value></value>
        public bool Private { get; set; }

        /// <summary>
        ///     Gets or sets the <see cref="CommandChannelDetails"/> item that provides further details on what commands can be run where.
        /// </summary>
        /// <value></value>
        public CommandChannelDetails Channels { get; set; }



    }

}