using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.BlackJack
{

    /// <summary>
    ///     Defines a Deck of playing cards
    /// </summary>
    public sealed class Deck
    {

        /// <summary>
        ///     Returns a Deck that 2 through Ace for all four suits
        /// </summary>
        public static List<Card> StandardDeck
        {
            get
            {
                var list = new List<Card>();
                foreach (var suit in Enum.GetValues<Suit>())
                {
                    foreach (var face in Enum.GetValues<Face>())
                    {
                        list.Add(new Card(face, suit));
                    }
                }
                return list;
            }
        }

        private readonly List<Card> deck;
        private readonly Random random;
        private Queue<Card> cards;

        /// <summary>
        ///     Creates a new Deck of cards that is composed of four decks of cards shuffled together
        /// </summary>
        /// <param name="random"><see cref="Random"/></param>
        public Deck(Random random = null)
        {
            deck = new List<Card>();
            deck.AddRange(StandardDeck);
            deck.AddRange(StandardDeck);
            deck.AddRange(StandardDeck);
            deck.AddRange(StandardDeck);
            this.random = random ?? new Random();
            Shuffle();
        }

        /// <summary>
        ///     Creates a new Deck of cards from the supplied collection of <see cref="Card"/>s that is then shuffled
        /// </summary>
        /// <param name="cards">A collection of <see cref="Card"/>s</param>
        /// <param name="random"><see cref="Random"/></param>
        public Deck(IEnumerable<Card> cards, Random random = null)
        {
            deck = cards.ToList();
            this.random = random ?? new Random();
            Shuffle();
        }

        /// <summary>
        ///     Shuffles the Deck of cards, optionally allowing a <see cref="Random"/> instance to be specified.
        /// </summary>
        /// <param name="random"><see cref="Random"/></param>
        public void Shuffle()
        {
            deck.Shuffle(random);
            cards = new(deck);
        }

        /// <summary>
        ///     Gets the next <see cref="Card"/>
        /// </summary>
        /// <returns><see cref="Card"/></returns>
        /// <remarks>
        ///     If the returned Card is the last card in the Deck, the Deck is shuffled.
        /// </remarks>
        public Card GetNextCard()
        {
            Card card = cards.Dequeue();
            if (cards.Count == 0)
            {
                Shuffle();
            }
            return card;
        }

        /// <summary>
        /// Creates a deck with a multiple of decks attached
        /// </summary>
        /// <param name="numberOfDecks">The number of decks. Must be greater than zero</param>
        /// <returns></returns>
        public static Deck CreateDeck(int numberOfDecks)
        {
            if (numberOfDecks <= 0) throw new ArgumentException("Must be more than zero decks", nameof(numberOfDecks));
            var cards = new List<Card>();
            for(var i = 0; i < numberOfDecks; i++)
            {
                cards.AddRange(StandardDeck);
            }
            return new Deck(cards);
        }

    }
}
