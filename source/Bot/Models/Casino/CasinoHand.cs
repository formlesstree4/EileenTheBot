using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.Casino
{

    /// <summary>
    ///     Defines a Hand in a BlackJack game
    /// </summary>
    public abstract class CasinoHand
    {

        /// <summary>
        /// Gets the User's hand
        /// </summary>
        public List<Card> Cards { get; } = new();

        /// <summary>
        /// Calculates the current value of the <see cref="CasinoHand"/>
        /// </summary>
        /// <returns></returns>
        public int Value => CalculateHandValue();

        /// <summary>
        /// Calculates the hidden dealer hand
        /// </summary>
        public int MaskedValue(int masked) => CalculateHandValue(Cards.Skip(masked).ToList());

        /// <summary>
        /// Gets the evaluation string to be used by the hand evaluation engine
        /// </summary>
        public string GetEvaluationString => string.Join(" ", Cards.Select(c => c.GetEvaluationString));

        /// <summary>
        /// Converts the hand to a representation
        /// </summary>
        /// <returns><see cref="string"/></returns>
        public override string ToString()
        {
            if (Cards.Count == 0) return "";
            if (Cards.Count == 1) return Cards[0].GetDisplayName;
            if (Cards.Count == 2) return $"{Cards[0].GetDisplayName} and {Cards[1].GetDisplayName}";
            return $"{string.Join(", ", Cards.Take(Cards.Count - 1).Select(h => h.GetDisplayName))}, and {Cards.TakeLast(1).First().GetDisplayName}";
        }

        protected abstract int CalculateHandValue(IList<Card> hand = null);

    }
}
