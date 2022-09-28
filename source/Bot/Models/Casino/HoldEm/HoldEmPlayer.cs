using Bot.Models.Currency;
using Bot.Models.Eileen;
using Discord;

namespace Bot.Models.Casino.HoldEm
{

    /// <summary>
    /// Defines a player that is sitting at a Texas Hold'Em table
    /// </summary>
    public sealed class HoldEmPlayer : CasinoPlayer<HoldEmHand>
    {

        /// <summary>
        /// Gets the <see cref="EileenCurrencyData"/> instance
        /// </summary>
        public EileenCurrencyData CurrencyData { get; }

        /// <summary>
        /// Gets or sets whether this player has the dealer button
        /// </summary>
        public bool HasDealerButton { get; set; } = false;

        /// <summary>
        /// Gets or sets if the current player pays the small blind
        /// </summary>
        public bool IsSmallBlind { get; set; } = false;

        /// <summary>
        /// Gets or sets if the current player pays the big blind
        /// </summary>
        public bool IsBigBlind { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the current player has folded
        /// </summary>
        public bool HasFolded { get; set; } = false;

        /// <summary>
        /// Gets the seat position this player is in, which is used to determine turn order
        /// </summary>
        public int SeatNumber { get; }


        public HoldEmPlayer(EileenUserData userData, EileenCurrencyData eileenCurrencyData, IUser user, int seatNumber) :
            base(userData, user)
        {
            CurrencyData = eileenCurrencyData;
            SeatNumber = seatNumber;
        }


        /// <summary>
        /// Gets a friendly display name for what kind of hand can currently be expressed with the given board
        /// </summary>
        /// <param name="board">The state of the board</param>
        /// <returns>A string name description</returns>
        public string GetCurrentBestHandName(string board)
        {
            int cards = 0;
            var pocket = Hand.GetEvaluationString;
            var handMask = HoldemHand.Hand.ParseHand(pocket, board, ref cards);
            return HoldemHand.Hand.DescriptionFromMask(handMask);
        }

        /// <summary>
        /// Gets an easy to use hand value that makes it simple to sort the winning players
        /// </summary>
        /// <param name="board">The state of the board</param>
        /// <returns>The unsigned value</returns>
        public uint GetHandValue(string board)
        {
            int cards = 0;
            var pocket = Hand.GetEvaluationString;
            var handMask = HoldemHand.Hand.ParseHand(pocket, board, ref cards);
            return HoldemHand.Hand.Evaluate(handMask, 5);
        }

        /// <summary>
        /// Calculates a player's chances in winning the current round
        /// </summary>
        /// <param name="board">The current state of the board</param>
        /// <returns>A percentage on whether or not the player will win</returns>
        public double CalculatePlayerOdds(string board)
        {
            int count = 0;
            double playerwins = 0.0;
            double opponentwins = 0.0;
            double[] player = new double[9];
            double[] opponent = new double[9];
            string pocket = Hand.GetEvaluationString;
            string hand = $"{pocket.Trim()} {board.Trim()}";

            if (!HoldemHand.Hand.ValidateHand(hand))
            {
                return double.NaN;
            }
            HoldemHand.Hand.ParseHand(hand, ref count);

            // Don't allow these configurations because of calculation time.
            if (count == 0 || count == 1 || count == 3 || count == 4 || count > 7)
            {
                return double.NaN;
            }

            HoldemHand.Hand.HandPlayerOpponentOdds(pocket, board, ref player, ref opponent);

            for (int i = 0; i < 9; i++)
            {
                playerwins += player[i] * 100.0;
                opponentwins += opponent[i] * 100.0;
            }

            return playerwins;
        }

        /// <summary>
        /// Gets the numerical sort value for this hand
        /// </summary>
        /// <returns></returns>
        public int GetSortNumber()
        {
            // In Texas Hold'Em, the turn order starts
            // immediately after the big blind. Therefore
            // the final three players to go, in order, are:
            // 1. Dealer Button
            // 2. Small Blind
            // 3. Big Blind
            if (IsBigBlind) return int.MaxValue;
            if (IsSmallBlind) return int.MaxValue - 1;
            if (HasDealerButton) return int.MaxValue - 2;
            return SeatNumber;
        }


    }

}
