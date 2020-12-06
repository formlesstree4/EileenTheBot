using System.Collections.Generic;
using System.Linq;
using Discord;

namespace Bot.Models.BlackJack
{

    /// <summary>
    ///     Represents an active game of BlackJack. It is a simple object model that the service will use to 
    /// </summary>
    public sealed class BlackJackGame
    {

        private int playerIndex = 0;

        /// <summary>
        ///     Gets the players for this game of BlackJack
        /// </summary>
        public List<EileenUserData> Players { get; }




        /// <summary>
        ///     Creates a new game
        /// </summary>
        public BlackJackGame()
        {
            Players = new List<EileenUserData>();
        }


        public void AddPlayer(EileenUserData userData) => Players.Add(userData);

        public bool IsPlaying(EileenUserData userData) => Players.Any(c => c.UserId == userData.UserId);

        public void RemovePlayer(EileenUserData userData) => Players.Remove(userData);

        public EileenUserData GetCurrentPlayer() => Players[playerIndex];

        public void AdvancePlayer() => playerIndex++;

        public bool HavePlayersFinished() => playerIndex >= Players.Count - 1;

    }

}