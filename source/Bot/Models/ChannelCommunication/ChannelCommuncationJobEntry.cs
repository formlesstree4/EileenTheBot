using System;

namespace Bot.Models.ChannelCommunication
{
    public sealed class ChannelCommuncationJobEntry
    {

        /// <summary>
        /// The unique job name
        /// </summary>
        public string JobName { get; set; }

        /// <summary>
        /// The guild ID where this job should run
        /// </summary>
        public ulong GuildId { get; set; }

        /// <summary>
        /// The channel ID where this job should run
        /// </summary>
        public ulong ChannelId { get; set; }

        /// <summary>
        /// When to run. If <see cref="Repeats"/> is true, this is a cron expression. Otherwise, number of minutes from now to run
        /// </summary>
        public string WhenToRun { get; set; }

        /// <summary>
        /// Does this job repeat or not
        /// </summary>
        public bool Repeats { get; set; }

        /// <summary>
        /// Has this job run
        /// </summary>
        public bool HasRun { get; set; } = false;

        /// <summary>
        /// The message that is to be communicated to the channel
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The datetime this was created
        /// </summary>
        public DateTime Created { get; set; }

    }
}
