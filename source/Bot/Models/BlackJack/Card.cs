namespace Bot.Models.BlackJack
{
    public sealed class Card
    {
        public Face Face { get; }

        public Suit Suit { get; }

        /// <summary>
        ///     Gets the represented value of this card.
        /// </summary>
        /// <remarks>
        ///     If <see cref="Face"/> is <see cref="Face.Ace"/> then <see cref="Value"/> returns 11. It will not ever return 1.
        /// </remarks>
        public int Value
        {
            get
            {
                return Face switch
                {
                    Face.Ace => 11,
                    Face.King or Face.Queen or Face.Jack => 10,
                    _ => (int)Face,
                };
            }
        }

        public Card(Face face, Suit suit)
        {
            Face = face;
            Suit = suit;
        }

        public string GetImageName => $"{FixTheFace(Face)}_of_{Suit}.png".ToLower();

        public string GetDisplayName => $"{Face} of {Suit}";

        private static string FixTheFace(Face face)
        {
            return face switch
            {
                Face.Ace or Face.King or Face.Queen or Face.Jack => face.ToString(),
                _ => ((int)face).ToString(),
            };
        }

    }

}
