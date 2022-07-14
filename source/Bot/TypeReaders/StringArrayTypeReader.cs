using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Bot.TypeReaders
{
    using Discord.Commands;

    public sealed class StringArrayTypeReader : TypeReader
    {

        private readonly Regex CommandParseExpression = new("[^\\s\"']+|\"([^\"]*)\"|'([^']*)'", RegexOptions.Compiled);

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

namespace Bot.TypeConverters
{
    using Discord;
    using Discord.Interactions;

    public sealed class StringArrayTypeConverter : TypeConverter
    {

        private readonly Regex CommandParseExpression = new("[^\\s\"']+|\"([^\"]*)\"|'([^']*)'", RegexOptions.Compiled);

        public override bool CanConvertTo(Type type)
        {
            return type == typeof(string[]);
        }

        public override ApplicationCommandOptionType GetDiscordType()
        {
            return ApplicationCommandOptionType.String;
        }


        public override Task<TypeConverterResult> ReadAsync(
            IInteractionContext context,
            IApplicationCommandInteractionDataOption option,
            IServiceProvider services)
        {
            var content = CommandParseExpression.Matches(context.Interaction.Data.ToString());
            var result = new List<string>();
            foreach (Match item in content)
            {
                var value = ApplyAdditionalEscapes(item.Value.Trim());
                if (string.IsNullOrWhiteSpace(value)) continue;
                result.Add(value);
            }
            return Task.FromResult(TypeConverterResult.FromSuccess(result.ToArray()));
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
