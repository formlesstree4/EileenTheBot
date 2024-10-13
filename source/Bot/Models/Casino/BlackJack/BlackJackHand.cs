using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.Casino.BlackJack
{

    /// <summary>
    /// Defines BlackJack specific properties for the <see cref="CasinoHand"/> class
    /// </summary>
    public sealed class BlackJackHand : CasinoHand
    {

        /// <summary>
        ///     Gets whether this hand is a bust.
        /// </summary>
        public bool IsBust => Value > 21;

        /// <summary>
        ///     Gets whether this hand is blackjack or not.
        /// </summary>
        public bool IsBlackJack => Cards.Count == 2 && Value == 21;

        /// <summary>
        ///     Gets whether this Hand can be split into two hands.
        /// </summary>
        public bool IsSplittable => Cards.Count == 2 && Cards[0].Face == Cards[1].Face;

        protected override int CalculateHandValue(IList<Card> hand = null)
        {
            hand ??= Cards;
            var total = hand.Where(c => c.Face != Face.Ace).Sum(c => c.Value);
            var aceCount = hand.Count(c => c.Face == Face.Ace);
            // This part is fun. You have to brain for a minute. I didn't when this was first written.
            // Here's the steps of ace handling.
            //  1) If we have no aces, return the calculated total.
            //  2) If the total, plus 10, plus ace count is greater than 21, return total + acecount.
            //      This fucking step might throw you for a bit. But that's OK. It threw me and my buddy here
            //      had to slowdown to fuckin turtle speed to explain why this was OK. This line basically
            //      treats ALL aces as one. It is pretty fucking obvious now but because I'm mentally handicapped
            //      it wasn't at first. Jesus Christ I need more alcohol (actually in hindsight, I need less).
            //  3) The last line is just the false part. Basically, treat the first ace like 11 and add 1 for all remainders. Magic.
            //      The others get treated like one automatically due to blackjack rules. You can choose if aces are 1 or 11 unless
            //      making them both 11 busts you. In which case you're retarded and the rules save you from fucking yourself in the ass.
            //      So we treat all remainder aces as 1. Get used to it. (11 * 2 > 21 = you busting like an idiot).
            if (aceCount == 0) return total;
            if (total + 10 + aceCount > 21) return total + aceCount;
            return total + 10 + aceCount;
        }

    }
}
