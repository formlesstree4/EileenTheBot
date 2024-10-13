using System.Collections.Generic;

namespace Bot.Models.Casino.HoldEm
{
    public sealed class HoldEmHand : CasinoHand
    {
        protected override int CalculateHandValue(IList<Card> hand = null)
        {
            throw new System.NotImplementedException();
        }
    }
}
