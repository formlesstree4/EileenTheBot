using System;

namespace Bot.Models.Eileen
{

    /// <summary>
    ///     Represents the current knowledge Eileen has about a User!
    /// </summary>
    public sealed class EileenUserData
    {
        public ulong UserId { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;

        public DateTime Updated { get; set; } = DateTime.Now;

        public ulong Money { get; set; } = 0;

        public ulong Loaned { get; set; } = 0;
    }

}
