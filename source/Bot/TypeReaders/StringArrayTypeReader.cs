using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Commands;

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
            foreach(Match item in content)
            {
                result.Add(item.Value);
            }
            return Task.FromResult(TypeReaderResult.FromSuccess(result.ToArray()));
        }
    }


}