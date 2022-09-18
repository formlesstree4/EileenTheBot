using Discord;
using System;
using System.Threading;

namespace Bot.Models.Casino
{

    /// <summary>
    /// Tracks table details in a single object
    /// </summary>
    /// <typeparam name="TTable">A subclass of <see cref="CasinoTable{TPlayer}"/></typeparam>
    /// <typeparam name="TPlayer">A subclass of <see cref="CasinoPlayer"/></typeparam>
    public abstract class CasinoTableDetails<TTable, TPlayer, THand>
        where TTable : CasinoTable<TPlayer, THand>
        where TPlayer : CasinoPlayer<THand>
        where THand : CasinoHand
    {

        private CancellationTokenSource _tokenSource;

        /// <summary>
        /// Gets the actual <typeparamref name="TTable"/> that the game is happening on
        /// </summary>
        public TTable Table { get; }

        /// <summary>
        /// Gets the Discord <see cref="IThreadChannel"/> where the game is happening
        /// </summary>
        public IThreadChannel ThreadChannel { get; }

        /// <summary>
        /// Gets or sets whether or not the game is currently running
        /// </summary>
        public bool IsThreadCurrentlyRunning { get; set; } = false;

        /// <summary>
        /// Gets or sets the current active <see cref="CancellationTokenSource"/>
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if attempting to set while <see cref="IsThreadCurrentlyRunning"/> equals true</exception>
        public CancellationTokenSource TokenSource
        {
            get => _tokenSource;
            set
            {
                if (IsThreadCurrentlyRunning)
                {
                    throw new InvalidOperationException($"You are not allowed to alter the {nameof(TokenSource)} property while a game is running");
                }
                _tokenSource = value;
            }
        }

        /// <summary>
        /// Creates a new <see cref="CasinoTableDetails{TTable, TPlayer}"/>
        /// </summary>
        /// <param name="table">A reference to <typeparamref name="TTable"/></param>
        /// <param name="threadChannel">A reference to a <see cref="IThreadChannel"/></param>
        /// <exception cref="ArgumentNullException"/>
        public CasinoTableDetails(TTable table, IThreadChannel threadChannel)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            ThreadChannel = threadChannel ?? throw new ArgumentNullException(nameof(threadChannel));
        }

    }
}
