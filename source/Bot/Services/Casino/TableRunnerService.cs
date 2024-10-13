using Bot.Models.Casino;
using Bot.Models.Eileen;
using Discord;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services.Casino
{
    public abstract class TableRunnerService<THand, TPlayer, TTable, TTableDetails> : IEileenService
        where THand : CasinoHand
        where TPlayer : CasinoPlayer<THand>
        where TTable : CasinoTable<TPlayer, THand>
        where TTableDetails : CasinoTableDetails<TTable, TPlayer, THand>
    {

        private readonly ConcurrentDictionary<ulong, TTableDetails> _tables = new();
        private readonly ILogger<TableRunnerService<THand, TPlayer, TTable, TTableDetails>> _logger;
        private readonly UserService _userService;


        /// <summary>
        /// Gets the ILogger associated with this runner service
        /// </summary>
        public ILogger<TableRunnerService<THand, TPlayer, TTable, TTableDetails>> Logger => _logger;

        /// <summary>
        /// Gets the internal dictionary which contains the collection of active tables
        /// </summary>
        /// <remarks>
        /// The Key is the ThreadId where the table is running. The value is the 
        /// </remarks>
        public ConcurrentDictionary<ulong, TTableDetails> Tables => _tables;



        public TableRunnerService(
            CancellationTokenSource cancellationTokenSource,
            ILogger<TableRunnerService<THand, TPlayer, TTable, TTableDetails>> logger,
            UserService userService)
        {
            _logger = logger;
            _userService = userService;
            cancellationTokenSource.Token.Register(() =>
            {
                foreach (var table in _tables)
                {
                    table.Value.TokenSource.Cancel();
                }
            });
        }


        /// <summary>
        /// Gets or creates a new <typeparamref name="TTable"/> for the given <see cref="IThreadChannel"/>
        /// </summary>
        /// <param name="threadChannel">The <see cref="IThreadChannel"/> to either locate an existing table for or create a new table for</param>
        /// <returns><typeparamref name="TTable"/></returns>
        public virtual TTable GetOrCreateTable(IThreadChannel threadChannel)
        {
            if (!_tables.TryGetValue(threadChannel.Id, out var t))
            {
                _logger.LogInformation("Creating a new BlackJack table for thread {threadId}", threadChannel.Id);
                var table = CreateNewTable(threadChannel);
                _tables.TryAdd(threadChannel.Id, CreateTableDetails(table, threadChannel));
                return table;
            }
            _logger.LogInformation("Returning an already created table for thread {threadId}", threadChannel.Id);
            return t.Table;
        }

        /// <summary>
        /// Starts up the table that is associated with the supplied <see cref="IThreadChannel"/>
        /// </summary>
        /// <param name="threadChannel">The <see cref="IThreadChannel"/> to start the loop for</param>
        /// <returns>Boolean value to indicate whether or not the thread has been started (or is already started)</returns>
        public virtual bool StartTableForChannel(IThreadChannel threadChannel)
        {
            _logger.LogInformation("Attempting to start the game loop for thread {threadId}", threadChannel.Id);
            if (!_tables.TryGetValue(threadChannel.Id, out var details)) return false;
            if (details.IsThreadCurrentlyRunning) return true;
            ThreadPool.QueueUserWorkItem(async tableDetails => { await TableRunnerLoop(tableDetails); }, details, false);
            _logger.LogInformation("The game loop for thread {threadId} has begun", threadChannel.Id);
            return true;
        }

        /// <summary>
        /// Attempts to halt a running game loop for the supplied <see cref="IThreadChannel"/>
        /// </summary>
        /// <param name="threadChannel">The <see cref="IThreadChannel"/> to stop</param>
        /// <remarks>Simple passthrough for <see cref="StopAndRemoveTable(ulong)"/></remarks>
        public virtual void StopAndRemoveTable(IThreadChannel threadChannel) =>
            StopAndRemoveTable(threadChannel.Id);

        /// <summary>
        /// Attempts to halt a running game loop for the supplied Thread ID and unloads the table instance data
        /// </summary>
        /// <param name="threadId">The thread ID to stop</param>
        public virtual void StopAndRemoveTable(ulong threadId)
        {
            if (_tables.TryGetValue(threadId, out var t))
            {
                _logger.LogInformation("Attempting to stop the game loop for thread {threadId}", threadId);
                t.TokenSource.Cancel();
                _tables.TryRemove(threadId, out _);
                _logger.LogInformation("A cancellation request for {threadId} has been initiated; logs will indicate if this was successful", threadId);
            }
        }

        /// <summary>
        ///     Safely adds a <see cref="IUser"/> to the supplied <see cref="TTable"/>
        /// </summary>
        /// <param name="table">The table to add the user to</param>
        /// <param name="user">A reference to the Discord <see cref="IUser"/></param>
        /// <returns>A promise that, if true, means the player was added successfully</returns>
        public virtual async Task<bool> AddPlayerSafelyToTable(TTable table, IUser user)
        {
            if (!table.PendingPlayers.Any(pp => pp.User.UserId == user.Id) &&
                !table.Players.Any(p => p.User.UserId == user.Id))
            {
                var userData = await _userService.GetOrCreateUserData(user);
                var blackJackPlayer = CreatePlayer(userData, user);
                if (table.IsGameActive)
                {
                    Logger.LogInformation("Adding {player} to the Pending Players collection", blackJackPlayer.Name);
                    table.PendingPlayers.Add(blackJackPlayer);
                }
                else
                {
                    Logger.LogInformation("Adding {player} to the Players collection", blackJackPlayer.Name);
                    table.Players.Add(blackJackPlayer);
                }
                return true;
            }
            Logger.LogInformation("{player} is already part of the game!", user.Username);
            return false;
        }

        /// <summary>
        ///     Safely removes a <see cref="IUser"/>
        /// </summary>
        /// <param name="table">The table to remove the user from</param>
        /// <param name="user">A reference to the Discord <see cref="IUser"/></param>
        /// <returns>A promise that, if true, means the player was removed successfully</returns>
        public virtual bool RemovePlayerSafelyFromTable(TTable table, IUser user)
        {
            if (table.IsGameActive && !table.LeavingPlayers.Any(lp => lp.User.UserId == user.Id))
            {
                Logger.LogInformation("Adding {player} to the Leaving Players collection", user.Username);
                table.LeavingPlayers.Add(table.Players.First(p => p.User.UserId == user.Id));
                return true;
            }
            if (!table.IsGameActive && table.Players.Any(p => p.User.UserId == user.Id))
            {
                Logger.LogInformation("Removing {player}...", user.Username);
                table.Players.Remove(table.Players.First(p => p.User.UserId == user.Id));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Creates a new <typeparamref name="TPlayer"/> using the supplied information
        /// </summary>
        /// <param name="userData">A reference to the <see cref="EileenUserData"/></param>
        /// <param name="user">A reference to the Discord <see cref="IUser"/></param>
        /// <returns><typeparamref name="TPlayer"/></returns>
        internal abstract TPlayer CreatePlayer(EileenUserData userData, IUser user);

        /// <summary>
        /// Creates a new <typeparamref name="TTable"/> for the given <see cref="IThreadChannel"/>
        /// </summary>
        /// <param name="threadChannel">The <see cref="IThreadChannel"/> to attach a table to</param>
        /// <returns><typeparamref name="TTable"/></returns>
        internal abstract TTable CreateNewTable(IThreadChannel threadChannel);

        /// <summary>
        /// Creates a new <typeparamref name="TTableDetails"/> that coordinates what table is associated to which channel
        /// </summary>
        /// <param name="table">The <typeparamref name="TTable"/> that has been created</param>
        /// <param name="channel">The associated <see cref="IThreadChannel"/></param>
        /// <returns><typeparamref name="TTableDetails"/></returns>
        internal abstract TTableDetails CreateTableDetails(TTable table, IThreadChannel channel);

        /// <summary>
        /// The loop that runs constantly behind-the-scenes to play the game
        /// </summary>
        /// <param name="tableDetails">The associated <typeparamref name="TTableDetails"/></param>
        /// <returns>A promise that can be awaited to run the game loop</returns>
        internal abstract Task TableRunnerLoop(TTableDetails tableDetails);

    }
}
