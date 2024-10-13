using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.Casino
{

    /// <summary>
    /// Defines a Deck of playing cards
    /// </summary>
    public sealed class Deck
    {

        /// <summary>
        /// Returns a Deck that contains 2 through Ace for all four suits
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

        private readonly List<Card> _deck;
        private readonly Random _random;
        private Queue<Card> _cards;

        /// <summary>
        /// Creates a new Deck of cards that is composed of four decks of cards shuffled together
        /// </summary>
        /// <param name="random">Optional <see cref="Random"/></param>
        public Deck(Random random = null)
        {
            _deck = new List<Card>();
            _deck.AddRange(StandardDeck);
            _deck.AddRange(StandardDeck);
            _deck.AddRange(StandardDeck);
            _deck.AddRange(StandardDeck);
            _random = random ?? new Random();
            Shuffle();
        }

        /// <summary>
        /// Creates a new Deck of cards from the supplied collection of <see cref="Card"/>s that is then shuffled
        /// </summary>
        /// <param name="cards">A collection of <see cref="Card"/>s</param>
        /// <param name="random">Optional <see cref="Random"/></param>
        public Deck(IEnumerable<Card> cards, Random random = null)
        {
            _deck = cards.ToList();
            _random = random ?? new Random();
            Shuffle();
        }

        /// <summary>
        /// Shuffles the Deck of cards, optionally allowing a <see cref="Random"/> instance to be specified.
        /// </summary>
        public void Shuffle()
        {
            _deck.Shuffle(_random);
            _cards = new(_deck);
        }

        /// <summary>
        /// Gets the next <see cref="Card"/>
        /// </summary>
        /// <returns><see cref="Card"/></returns>
        /// <remarks>
        /// If the returned Card is the last card in the Deck, the Deck is shuffled.
        /// </remarks>
        public Card GetNextCard()
        {
            Card card = _cards.Dequeue();
            if (_cards.Count == 0)
            {
                Shuffle();
            }
            return card;
        }

        /// <summary>
        /// Creates a deck with a multiple of decks attached
        /// </summary>
        /// <param name="numberOfDecks">The number of decks to combine together</param>
        /// <returns><see cref="Deck"/></returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="numberOfDecks"/> is less than or equal to zero</exception>
        public static Deck CreateDeck(int numberOfDecks)
        {
            if (numberOfDecks <= 0) throw new ArgumentException("Must be more than zero decks", nameof(numberOfDecks));
            var cards = new List<Card>();
            for (var i = 0; i < numberOfDecks; i++)
            {
                cards.AddRange(StandardDeck);
            }
            return new Deck(cards);
        }

    }
}
