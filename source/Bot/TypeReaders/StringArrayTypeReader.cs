using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Bot.TypeReaders
{

    public sealed class StringArrayTypeReader : TypeReader
    {

        private readonly Regex CommandParseExpression = new Regex("[^\\s\"']+|\"([^\"]*)\"|'([^']*)'", RegexOptions.Compiled);

        public override Task<TypeReaderResult> ReadAsync(
            ICommandContext context,
            string input,
            IServiceProvider services)
        {
            var content = CommandParseExpression.Matches(input);
            var result = new List<string>();
            foreach (Match item in content)
            {
                var value = ApplyAdditionalEscapes(item.Value.Trim());
                if (string.IsNullOrWhiteSpace(value)) continue;
                result.Add(value);
            }
            return Task.FromResult(TypeReaderResult.FromSuccess(result.ToArray()));
        }

        /// <summary>
        ///     You know who you are.
        /// </summary>
        /// <param name="input">The input string to attempt additional sanitizations with</param>
        /// <returns>Cleaned up string</returns>
        private string ApplyAdditionalEscapes(string input)
        {
            return input.Replace("\u200B", "");
        }

    }

}