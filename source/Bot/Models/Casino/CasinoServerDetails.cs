namespace Bot.Models.Casino
{

    /// <summary>
    /// Houses server related details about the casino
    /// </summary>
    public abstract class CasinoServerDetails
    {

        /// <summary>
        /// Gets or sets the Channel ID where a table can be created
        /// </summary>
        public ulong? ChannelId { get; set; } = null;

    }

}
