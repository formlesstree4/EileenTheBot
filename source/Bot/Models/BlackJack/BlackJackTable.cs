using Bot.Services;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Models.BlackJack
{


    public sealed class BlackJackTable
    {

        private readonly Stack<BlackJackPlayer> currentRoundPlayers = new();
        private readonly Queue<BlackJackPlayer> finishedRoundPlayers = new();


        /// <summary>
        ///     Gets the unique Table ID
        /// </summary>
        /// <remarks>
        ///     This may not even stick around; it's really not very useful
        /// </remarks>
        public Guid TableId { get; } = Guid.NewGuid();

        /// <summary>
        ///     Gets whether or not the table is currently playing a game.
        /// </summary>
        public bool IsGameActive { get; set; } = false;

        /// <summary>
        ///     Gets the Dealer for this table
        /// </summary>
        public BlackJackPlayer Dealer { get; } = new(null, null);

        /// <summary>
        ///     Gets the list of current active players
        /// </summary>
        public List<BlackJackPlayer> Players { get; } = new();

        /// <summary>
        ///     Gets the list of players that are waiting to join on the next round.
        /// </summary>
        public List<BlackJackPlayer> PendingPlayers { get; } = new();

        /// <summary>
        ///     Gets the list of players that are waiting to leave at the end of the round.
        /// </summary>
        public List<BlackJackPlayer> LeavingPlayers { get; } = new();

        /// <summary>
        ///     Gets the <see cref="Deck"/> used at the table.
        /// </summary>
        public Deck Deck { get; private set; }



        /// <summary>
        ///     Creates a new <see cref="BlackJackTable"/>
        /// </summary>
        /// <param name="deck">The Deck of cards to use indefinitely for this table</param>
        public BlackJackTable(Deck deck)
        {
            Deck = deck;
        }



        /// <summary>
        ///     Returns whether or not the given <see cref="BlackJackPlayer"/> can alter their bet
        /// </summary>
        /// <param name="player"><see cref="BlackJackPlayer"/></param>
        /// <returns>true/false if their Bet can be changed</returns>
        public bool CanPlayerAlterBet(BlackJackPlayer player)
        {
            return CanPlayerAlterBet(player.User.UserId);
        }

        /// <summary>
        ///     Returns whether or not the given player ID can alter their bet
        /// </summary>
        /// <param name="playerId">The Discord snowflake ID</param>
        /// <returns>true/false if their Bet can be changed</returns>
        public bool CanPlayerAlterBet(ulong playerId)
        {
            return IsGameActive
                ? PendingPlayers.Any(c => c.User.UserId == playerId) || currentRoundPlayers.Any(c => c.User.UserId == playerId)
                : PendingPlayers.Any(c => c.User.UserId == playerId) || Players.Any(c => c.User.UserId == playerId);
        }

        /// <summary>
        ///     Gets the next player in the round to go
        /// </summary>
        /// <returns>If true, <paramref name="nextPlayer"/> is a Player. If false, <paramref name="nextPlayer"/> is the Dealer</returns>
        public bool GetNextPlayer(out BlackJackPlayer nextPlayer)
        {
            if (currentRoundPlayers.Count == 0)
            {
                nextPlayer = Dealer;
                return false;
            }
            nextPlayer = currentRoundPlayers.Pop();
            finishedRoundPlayers.Enqueue(nextPlayer);
            return true;
        }

        /// <summary>
        /// Looks for a <see cref="BlackJackPlayer"/> that's either pending to play or playing
        /// </summary>
        /// <param name="playerId">The Player ID to look for</param>
        /// <returns><see cref="BlackJackPlayer"/> if discovered</returns>
        public BlackJackPlayer FindPlayer(ulong playerId)
        {
            return PendingPlayers.FirstOrDefault(c => c.User.UserId == playerId) ??
                Players.FirstOrDefault(c => c.User.UserId == playerId);
        }

        /// <summary>
        ///     Adds a Player, who is a clone of another Player, to the Stack of Players
        /// </summary>
        /// <param name="splitHandPlayer"><see cref="BlackJackPlayer"/></param>
        public void InsertSplitPlayerOntoStack(BlackJackPlayer splitHandPlayer)
        {
            currentRoundPlayers.Push(splitHandPlayer);
            Players.Add(splitHandPlayer);
        }

        /// <summary>
        ///     Gets the <see cref="BlackJackPlayer"/> in the order they played the round in so the post-round processing can occur.
        /// </summary>
        /// <returns>A collection of <see cref="BlackJackPlayer"/></returns>
        /// <remarks>This is not multi-iterable friendly</remarks>
        public IEnumerable<BlackJackPlayer> GetPlayersForEndOfRoundProcessing()
        {
            while(finishedRoundPlayers.Count > 0)
            {
                yield return finishedRoundPlayers.Dequeue();
            }
        }

        /// <summary>
        ///     Sets up the table internally for the next round
        /// </summary>
        /// <remarks>
        ///     The Runner Service is responsible for the manipulation of <see cref="PendingPlayers"/> and <see cref="LeavingPlayers"/>
        /// </remarks>
        public void SetupTable()
        {
            foreach (var player in Players.Reverse<BlackJackPlayer>())
            {
                currentRoundPlayers.Push(player);
            }
        }

    }

}
