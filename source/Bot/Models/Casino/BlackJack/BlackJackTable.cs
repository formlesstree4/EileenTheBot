using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.Casino.BlackJack
{


    public sealed class BlackJackTable : CasinoTable<BlackJackPlayer>
    {

        /// <summary>
        ///     Creates a new <see cref="BlackJackTable"/>
        /// </summary>
        /// <param name="deck">The Deck of cards to use indefinitely for this table</param>
        public BlackJackTable(Deck deck) :
            base (new(null, null), deck)
        { }



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
                ? PendingPlayers.Any(c => c.User.UserId == playerId) || CurrentRoundPlayers.Any(c => c.User.UserId == playerId)
                : PendingPlayers.Any(c => c.User.UserId == playerId) || Players.Any(c => c.User.UserId == playerId);
        }

        /// <summary>
        ///     Adds a Player, who is a clone of another Player, to the Stack of Players
        /// </summary>
        /// <param name="splitHandPlayer"><see cref="BlackJackPlayer"/></param>
        public void InsertSplitPlayerOntoStack(BlackJackPlayer splitHandPlayer)
        {
            CurrentRoundPlayers.Push(splitHandPlayer);
            Players.Add(splitHandPlayer);
        }

        /// <summary>
        ///     Gets the <see cref="BlackJackPlayer"/> in the order they played the round in so the post-round processing can occur.
        /// </summary>
        /// <returns>A collection of <see cref="BlackJackPlayer"/></returns>
        /// <remarks>This is not multi-iterable friendly</remarks>
        public IEnumerable<BlackJackPlayer> GetPlayersForEndOfRoundProcessing()
        {
            while(FinishedRoundPlayers.Count > 0)
            {
                yield return FinishedRoundPlayers.Dequeue();
            }
        }

    }

}
