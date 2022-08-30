using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.BlackJack
{
    public sealed class Deck
    {
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
        private Queue<Card> cards;

        public Deck()
        {
            deck = new List<Card>();
            deck.AddRange(StandardDeck);
            deck.AddRange(StandardDeck);
            deck.AddRange(StandardDeck);
            deck.AddRange(StandardDeck);
            Shuffle();
        }

        public Deck(IEnumerable<Card> cards)
        {
            deck = cards.ToList();
            Shuffle();
        }


        public void Shuffle(Random random = null)
        {
            deck.Shuffle(random ?? new Random());
            cards = new(deck);
        }

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
