namespace Bot.Services.Markov
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Some code used from https://github.com/otac0n/markov 
    // I have modified it to my needs.

    public sealed class MarkovChain<T> where T : IEquatable<T>
    {
        private Dictionary<ChainState<T>, Dictionary<T, int>> items;
        private Dictionary<ChainState<T>, int> terminals;
        private int order;
        private Random random;

        public int Order => order;
        public ulong Size => (ulong)items.Count + (ulong)terminals.Count;

        public MarkovChain() : this(new Random()) { }

        public MarkovChain(Random random) : this(1, random) { }

        public MarkovChain(int ordering, Random random_instance)
        {
            if (ordering < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordering));
            }

            items = new Dictionary<ChainState<T>, Dictionary<T, int>>();
            terminals = new Dictionary<ChainState<T>, int>();

            order = ordering;
            random = random_instance;
        }

        public MarkovChain(int ordering, int seed) : this(ordering, new Random(seed)) { }

        public MarkovChain(int ordering) : this(ordering, new Random()) { }

        public void Add(IEnumerable<T> items) => Add(items, 1);

        public void Add(IEnumerable<T> items, int weight)
        {
            var previous = new Queue<T>();
            foreach (var item in items)
            {
                var key = new ChainState<T>(previous);

                AddInternal(key, item, weight);

                previous.Enqueue(item);
                if (previous.Count > order)
                {
                    previous.Dequeue();
                }
            }

            AddTerminalInternal(new ChainState<T>(previous), weight);
        }

        public void Add(IEnumerable<T> previous, T item)
        {
            var state = new Queue<T>(previous);
            while (state.Count > order)
            {
                state.Dequeue();
            }

            AddInternal(new ChainState<T>(state), item, 1);
        }

        public void Add(IEnumerable<T> previous, T item, int weight)
        {
            var state = new Queue<T>(previous);
            while (state.Count > order)
            {
                state.Dequeue();
            }

            AddInternal(new ChainState<T>(state), item, weight);
        }

        public IEnumerable<T> Walk() => Walk(Enumerable.Empty<T>(), random);

        public IEnumerable<T> Walk(Random rand) => Walk(Enumerable.Empty<T>(), rand);

        public IEnumerable<T> Walk(IEnumerable<T> previous) => Walk(previous, random);

        public IEnumerable<T> Walk(IEnumerable<T> previous, Random rand)
        {
            var state = new Queue<T>(previous);

            while (true)
            {
                while (state.Count > order)
                {
                    state.Dequeue();
                }

                var key = new ChainState<T>(state);

                var weights = GetNextStatesHelper(key);
                if (weights == null)
                {
                    yield break;
                }

                var terminalWeight = GetTerminalWeightInternal(key);

                var total = weights.Sum(w => w.Value);
                var value = rand.Next(total + terminalWeight) + 1;

                if (value > total)
                {
                    yield break;
                }

                var currentWeight = 0;
                foreach (var nextItem in weights)
                {
                    currentWeight += nextItem.Value;
                    if (currentWeight >= value)
                    {
                        yield return nextItem.Key;
                        state.Enqueue(nextItem.Key);
                        break;
                    }
                }
            }
        }




        // --- PRIVATE --- //

        private void AddInternal(ChainState<T> state, T next, int weight)
        {
            if (!items.TryGetValue(state, out var weights))
            {
                weights = new Dictionary<T, int>();
                items.Add(state, weights);
            }

            var newWeight = Math.Max(0, weights.ContainsKey(next)
                ? weight + weights[next]
                : weight);
            if (newWeight == 0)
            {
                weights.Remove(next);
                if (weights.Count == 0)
                {
                    items.Remove(state);
                }
            }
            else
            {
                weights[next] = newWeight;
            }
        }

        private int GetTerminalWeightInternal(ChainState<T> state)
        {
            terminals.TryGetValue(state, out var weight);
            return weight;
        }

        private void AddTerminalInternal(ChainState<T> state, int weight)
        {
            weight = Math.Max(0, terminals.ContainsKey(state)
                ? weight + terminals[state]
                : weight);

            if (weight == 0)
            {
                terminals.Remove(state);
            }
            else
            {
                terminals[state] = weight;
            }
        }

#nullable enable
        private Dictionary<T, int>? GetNextStatesHelper(ChainState<T> state) =>
            items.TryGetValue(state, out var weights)
                ? weights
                : null;
#nullable disable

        private class ChainState<CT> : IEquatable<ChainState<CT>>
        {
            private readonly CT[] items;

            public ChainState(IEnumerable<CT> t_items)
            {
                items = t_items.ToArray();
            }

            public ChainState(params CT[] t_items)
            {
                items = new CT[t_items.Length];
                Array.Copy(t_items, items, t_items.Length);
            }

            public static bool operator ==(ChainState<CT> a, ChainState<CT> b)
            {
                if (object.ReferenceEquals(a, b))
                {
                    return true;
                }
                else if (a is null)
                {
                    return false;
                }

                return a.Equals(b);
            }

            public static bool operator !=(ChainState<CT> a, ChainState<CT> b)
            {
                return !(a == b);
            }

#nullable enable
            public override bool Equals(object? obj)
            {
                if (obj is ChainState<CT> chain)
                {
                    return Equals(chain);
                }

                return false;
            }


            public bool Equals(ChainState<CT>? other)
            {
                if (other is null)
                {
                    return false;
                }

                if (items.Length != other.items.Length)
                {
                    return false;
                }

                for (var i = 0; i < items.Length; i++)
                {
                    if (!items[i]!.Equals(other.items[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
#nullable disable

            public override int GetHashCode()
            {
                var code = items.Length;

                for (var i = 0; i < items.Length; i++)
                {
                    code = (code * 37) + items[i]!.GetHashCode();
                }

                return code;
            }
        }

    }
}
