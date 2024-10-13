using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.Casino
{
    /// <summary>
    /// Contains the basic casino table logic across multiple types of Poker
    /// </summary>
    /// <typeparam name="TPlayer">A subclass of <see cref="CasinoPlayer{THand}"/></typeparam>
    /// <typeparam name="THand">A subclass of <see cref="CasinoHand"/></typeparam>
    public abstract class CasinoTable<TPlayer, THand>
        where TPlayer : CasinoPlayer<THand>
        where THand: CasinoHand
    {

        private readonly Stack<TPlayer> _currentRoundPlayers = new();
        private readonly Queue<TPlayer> _finishedRoundPlayers = new();

        /// <summary>
        ///     Gets the unique Table ID
        /// </summary>
        /// <remarks>
        ///     This may not even stick around; it's really not very useful
        /// </remarks>
        public Guid TableId { get; } = Guid.NewGuid();

        /// <summary>
        ///     Gets the Dealer for this table
        /// </summary>
        public TPlayer Dealer { get; }

        /// <summary>
        ///     Gets the list of current active players
        /// </summary>
        public List<TPlayer> Players { get; } = new();

        /// <summary>
        ///     Gets the list of players that are waiting to join on the next round.
        /// </summary>
        public List<TPlayer> PendingPlayers { get; } = new();

        /// <summary>
        ///     Gets the list of players that are waiting to leave at the end of the round.
        /// </summary>
        public List<TPlayer> LeavingPlayers { get; } = new();

        /// <summary>
        ///     Gets the <see cref="Deck"/> used at the table.
        /// </summary>
        public Deck Deck { get; private set; }

        /// <summary>
        ///     Gets whether or not the table is currently playing a game.
        /// </summary>
        public bool IsGameActive { get; set; } = false;

        /// <summary>
        /// Gets the <see cref="Stack{TPlayer}"/> of players that are yet to go for this round
        /// </summary>
        public Stack<TPlayer> CurrentRoundPlayers => _currentRoundPlayers;

        /// <summary>
        /// Gets the <see cref="Queue{TPlayer}"/> of players that have already gone for this round
        /// </summary>
        public Queue<TPlayer> FinishedRoundPlayers => _finishedRoundPlayers;



        /// <summary>
        ///     Creates a new instance of a CasinoTable with the given Dealer and Deck of Cards to use for the table
        /// </summary>
        /// <param name="dealer"><see cref="TPlayer"/></param>
        /// <param name="deck"><see cref="Casino.Deck"/></param>
        public CasinoTable(TPlayer dealer, Deck deck)
        {
            Dealer = dealer;
            Deck = deck;
        }



        /// <summary>
        ///     Gets the next player in the round to go
        /// </summary>
        /// <returns>If true, <paramref name="nextPlayer"/> is a Player. If false, <paramref name="nextPlayer"/> is the Dealer</returns>
        public bool GetNextPlayer(out TPlayer nextPlayer)
        {
            if (_currentRoundPlayers.Count == 0)
            {
                nextPlayer = Dealer;
                return false;
            }
            nextPlayer = _currentRoundPlayers.Pop();
            _finishedRoundPlayers.Enqueue(nextPlayer);
            return true;
        }

        /// <summary>
        /// Looks for a <see cref="TPlayer"/> that's either pending to play or playing
        /// </summary>
        /// <param name="playerId">The Player ID to look for</param>
        /// <returns><see cref="TPlayer"/> if discovered</returns>
        public TPlayer FindPlayer(ulong playerId)
        {
            return PendingPlayers.FirstOrDefault(c => c.User.UserId == playerId) ??
                Players.FirstOrDefault(c => c.User.UserId == playerId);
        }

        /// <summary>
        /// Populates the internal stack of players
        /// </summary>
        public virtual void PopulateTableStack()
        {
            foreach (var player in Players.Reverse<TPlayer>())
            {
                _currentRoundPlayers.Push(player);
            }
        }

    }
}
