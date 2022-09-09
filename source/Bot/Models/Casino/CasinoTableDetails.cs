using Discord;
using System.Threading;

namespace Bot.Models.Casino
{
    public abstract class CasinoTableDetails<TTable, TPlayer>
        where TTable : CasinoTable<TPlayer>
        where TPlayer : CasinoPlayer
    {

        public TTable Table { get; }

        public IThreadChannel ThreadChannel { get; }

        public bool IsThreadCurrentlyRunning { get; set; } = false;

        public CancellationTokenSource CancellationTokenSource { get; set; } = new();

        public CasinoTableDetails(TTable table, IThreadChannel threadChannel)
        {
            Table = table ?? throw new System.ArgumentNullException(nameof(table));
            ThreadChannel = threadChannel ?? throw new System.ArgumentNullException(nameof(threadChannel));
        }

    }
}
