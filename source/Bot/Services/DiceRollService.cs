using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Bot.Services
{

    /// <summary>
    ///     Specifies additional processing options when a dice roll is being parsed.
    /// </summary>
    public enum DiceExpressionOptions
    {

        /// <summary>
        ///     No additional options are applied to the dice strong.
        /// </summary>
        None,

        /// <summary>
        ///     Attempts to simplify the dice string.
        /// </summary>
        SimplifyStringValue

    }

    public sealed class DiceRollService : IEileenService
    {

        /// <summary>
        ///     Gets a <see cref="DiceExpression"/> for the given string
        /// </summary>
        /// <param name="expression">The string expression to convert</param>
        /// <returns><see cref="DiceExpression"/></returns>
        public static DiceExpression GetDiceExpression(string expression) => new(expression, DiceExpressionOptions.SimplifyStringValue);

    }

    /// <summary>
    ///     I found this somewhere on the Internet. It's awesome. Whoever made this is a saint.
    /// </summary>
    /// <remarks>
    /// <![CDATA[
    /// <expr> :=   <expr> + <expr>
    ///  | <expr> - <expr>
    ///  | [<number>]d(<number>|%)
    ///  | <number>
    ///    <number> := positive integer
    /// ]]>

    /// </remarks>
    public sealed class DiceExpression
    {

        /// <summary>
        ///     Gets a <see cref="DiceExpression"/> that always evaluates to zero.
        /// </summary>
        public static DiceExpression Zero { get; } = new DiceExpression("0");


        private readonly Regex _numberToken = new("^[0-9]+$", RegexOptions.Compiled);
        private readonly Regex _diceRollToken = new("^([0-9]*)d([0-9]+|%)$", RegexOptions.Compiled);
        private readonly List<KeyValuePair<long, IDiceExpressionNode>> _nodes = new();



        /// <summary>
        ///     Gets a readonly collection of the parsed <see cref="IDiceExpressionNode"/>.
        /// </summary>
        /// <remarks>
        ///     The key in the returned <see cref="KeyValuePair{TKey, TValue}"/> indicates whether the <see cref="IDiceExpressionNode"/> associated with it is to be added or subtracted from the running total.
        /// </remarks>
        public IReadOnlyCollection<KeyValuePair<long, IDiceExpressionNode>> Expressions => _nodes.AsReadOnly();


        /// <summary>
        ///     Creates a new instance of the <see cref="DiceExpression"/> class.
        /// </summary>
        /// <param name="expression">The string expression to parse.</param>
        /// <param name="options"><see cref="DiceExpressionOptions"/></param>
        public DiceExpression(string expression, DiceExpressionOptions options = DiceExpressionOptions.None)
        {
            // A well-formed dice expression's tokens will be either +, -, an integer, or XdY.
            var tokens = expression.Replace("+", " + ").Replace("-", " - ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Blank dice expressions end up being DiceExpression.Zero.
            if (!tokens.Any())
            {
                tokens = new[] { "0" };
            }

            // Since we parse tokens in operator-then-operand pairs, make sure the first token is an operand.
            if (tokens[0] != "+" && tokens[0] != "-")
            {
                tokens = (new[] { "+" }).Concat(tokens).ToArray();
            }

            // This is a precondition for the below parsing loop to make any sense.
            if (tokens.Length % 2 != 0)
            {
                throw new ArgumentException("The given dice expression was not in an expected format: even after normalization, it contained an odd number of tokens.");
            }

            // Parse operator-then-operand pairs into nodes.
            for (long tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex += 2)
            {
                var token = tokens[tokenIndex];
                var nextToken = tokens[tokenIndex + 1];

                if (token != "+" && token != "-")
                {
                    throw new ArgumentException("The given dice expression was not in an expected format.");
                }
                long multiplier = token == "+" ? +1 : -1;

                if (_numberToken.IsMatch(nextToken))
                {
                    _nodes.Add(new KeyValuePair<long, IDiceExpressionNode>(multiplier, new NumberNode(long.Parse(nextToken))));
                }
                else if (_diceRollToken.IsMatch(nextToken))
                {
                    var match = _diceRollToken.Match(nextToken);
                    long numberOfDice = Math.Min(10, match.Groups[1].Value == string.Empty ? 1 : long.Parse(match.Groups[1].Value));
                    long diceType = Math.Min(100, match.Groups[2].Value == "%" ? 100 : long.Parse(match.Groups[2].Value));
                    _nodes.Add(new KeyValuePair<long, IDiceExpressionNode>(multiplier, new DiceRollNode(numberOfDice, diceType)));
                }
                else
                {
                    throw new ArgumentException("The given dice expression was not in an expected format: the non-operand token was neither a number nor a dice-roll expression.");
                }
            }

            // Sort the nodes in an aesthetically-pleasing fashion.
            var diceRollNodes = _nodes.Where(pair => pair.Value is DiceRollNode)
                                          .OrderByDescending(node => node.Key)
                                          .ThenByDescending(node => ((DiceRollNode)node.Value).DiceType)
                                          .ThenByDescending(node => ((DiceRollNode)node.Value).NumberOfDice)
                                          .ToList();
            var numberNodes = _nodes.Where(pair => pair.Value is NumberNode)
                                        .OrderByDescending(node => node.Key)
                                        .ThenByDescending(node => node.Value.Evaluate())
                                        .ToList();

            // If desired, merge all number nodes together, and merge dice nodes of the same type together.
            if (options == DiceExpressionOptions.SimplifyStringValue)
            {
                long number = numberNodes.Sum(pair => pair.Key * pair.Value.Evaluate());
                var diceTypes = diceRollNodes.Select(node => ((DiceRollNode)node.Value).DiceType).Distinct();
                var normalizedDiceRollNodes = from type in diceTypes
                                              let numDiceOfThisType = diceRollNodes.Where(node => ((DiceRollNode)node.Value).DiceType == type).Sum(node => node.Key * ((DiceRollNode)node.Value).NumberOfDice)
                                              where numDiceOfThisType != 0
                                              let multiplicand = numDiceOfThisType > 0 ? +1 : -1
                                              let absNumDice = Math.Abs(numDiceOfThisType)
                                              orderby multiplicand descending, type descending
                                              select new KeyValuePair<long, IDiceExpressionNode>(multiplicand, new DiceRollNode(absNumDice, type));

                _nodes = (number == 0 ? normalizedDiceRollNodes
                                          : normalizedDiceRollNodes.Concat(new[] { new KeyValuePair<long, IDiceExpressionNode>(number > 0 ? +1 : -1, new NumberNode(number)) })).ToList();
            }
            // Otherwise, just put the dice-roll nodes first, then the number nodes.
            else
            {
                _nodes = diceRollNodes.Concat(numberNodes).ToList();
            }
        }



        /// <summary>
        ///     Evaluates the <see cref="DiceExpression"/>.
        /// </summary>
        /// <returns><see cref="long"/></returns>
        /// <remarks>
        ///     Effectively, <see cref="Evaluate"/> rolls the dice.
        /// </remarks>
        public long Evaluate()
        {
            long result = 0;
            foreach (var pair in _nodes)
            {
                result += pair.Key * pair.Value.Evaluate();
            }
            return result;
        }


        public Dictionary<string, List<long>> EvaluateWithDetails()
        {
            var result = new Dictionary<string, List<long>>();
            foreach (var pair in _nodes)
            {
                // result += pair.Key * pair.Value.Evaluate();
                result.Add($"{pair.Value}", pair.Value.EvaluateWithDetails());
            }
            return result;
        }

        /// <summary>
        ///     Returns a calculated average of the <see cref="DiceExpression"/>
        /// </summary>
        /// <returns><see cref="decimal"/></returns>
        public decimal GetCalculatedAverage()
        {
            decimal result = 0;
            foreach (var pair in _nodes)
            {
                result += pair.Key * pair.Value.GetCalculatedAverage();
            }
            return result;
        }

        /// <summary>
        ///     Returns the string equivalent of <see cref="DiceExpression"/>.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var result = (_nodes[0].Key == -1 ? "-" : string.Empty) + _nodes[0].Value;
            foreach (var pair in _nodes.Skip(1))
            {
                result += pair.Key == +1 ? " + " : " − "; // NOTE: unicode minus sign, not hyphen-minus '-'.
                result += pair.Value.ToString();
            }
            return result;
        }



        public interface IDiceExpressionNode
        {
            long Evaluate();
            List<long> EvaluateWithDetails();
            decimal GetCalculatedAverage();
        }

        private sealed class NumberNode : IDiceExpressionNode
        {
            private readonly long _theNumber;
            public NumberNode(long theNumber)
            {
                _theNumber = theNumber;
            }
            public long Evaluate()
            {
                return _theNumber;
            }

            public List<long> EvaluateWithDetails()
            {
                return new List<long> { _theNumber };
            }

            public decimal GetCalculatedAverage()
            {
                return _theNumber;
            }
            public override string ToString()
            {
                return _theNumber.ToString();
            }
        }
        public sealed class DiceRollNode : IDiceExpressionNode
        {
            private static readonly Random Roller = new Random();

            public DiceRollNode(long numberOfDice, long diceType)
            {
                NumberOfDice = numberOfDice;
                DiceType = diceType;
            }

            public long Evaluate()
            {
                long total = 0;
                for (long i = 0; i < NumberOfDice; ++i)
                {
                    total += Roller.Next(1, (int)DiceType + 1);
                }
                return total;
            }

            public List<long> EvaluateWithDetails()
            {
                var result = new List<long>();
                for (long i = 0; i < NumberOfDice; ++i)
                {
                    result.Add(Roller.Next(1, (int)DiceType + 1));
                }
                return result;
            }

            public decimal GetCalculatedAverage()
            {
                return NumberOfDice * ((DiceType + 1.0m) / 2.0m);
            }

            public override string ToString()
            {
                return $"{NumberOfDice}d{DiceType}";
            }

            public long NumberOfDice { get; }

            public long DiceType { get; }
        }
    }
}
