using Discord.Interactions;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Bot.Modules
{
    public sealed class OwoModule : InteractionModuleBase
    {
        private static readonly string[] s_faces = {
            "(\x30FB`\x03C9\x00B4\x30FB)",
            ";;w;;",
            "owo",
            "UwU",
            ">w<",
            "^w^",
            "◕w◕",
            "(⁄ʘ⁄⁄ω⁄⁄ʘ⁄)♡",
            "*𝓌𝒶𝓉𝓈 𝒹𝒾𝓈?*ღ(O꒳Oღ)",
            "( ͡o ꒳ ͡o )*𝔫𝔬𝔱𝔦𝔠𝔢𝔰 𝔟𝔲𝔩𝔤𝔢*",
            "◔w◔",
            "︠ʘw ︠ʘ",
            "（ ゜ω 。）",
            "（ 。ω ゜）",
            "( °꒳° )",
            "‿︵*𝓇𝒶𝓌𝓇*‿︵ ʘwʘ",
            "♥ ⑅  𝒘𝒉𝒆𝒓𝒆 (⦿   ᳕ ⦿) 𝒓 𝒖 ? ⑅ ♥"
        };

        private readonly Random rng;


        public OwoModule(Random random)
        {
            rng = random;
        }

        

        [SlashCommand("owo", "Crafts an amalgamation of text like if a furry said it")]
        public async Task Owoify([Summary("text", "The text to owoify")]string text)
        {
            var output = OwoifyText(text);
            await RespondAsync(output);
        }

        private string OwoifyText(string text)
        {
            switch (text.ToLower()) // Because case sensitivity
            {
                case "owo uwu owo":
                    return "human trafficking"; // https://cdn.discordapp.com/attachments/571064920808882176/573230351284043776/unknown.png
                case "owo uwu owo uwu":
                    return "no one can afford it";
                case "owo uwu owo uwu owo":
                    return "no one can afford to hire a person";
                case "owo uwu owo uwu owo uwu":
                    return "no one can afford to be lost";
                case "owo uwu owo uwu owo uwu owo":
                    return "no one can afford to buy a man";
                case "owo uwu owo uwu owo uwu owo uwu":
                    return "a traveler does not want to be lonely";
                case "owo uwu owo uwu owo uwu owo uwu owo":
                    return "no one can afford to be a victim of poverty";
                case "owo uwu owo uwu owo uwu owo uwu owo uwu":
                    return "no one can afford to be lonely";
                case "owo uwu owo uwu owo uwu owo uwu owo uwu owo":
                    return "a person who does not want to be able to find himself in a state of emergency does not have to be a victim of human suffering";
            }

            var result = Regex.Replace(text, "[rl]", "w"); // Replace 'r' or 'l' with 'w'
            result = Regex.Replace(result, "[RL]", "W"); // Same as above, but for upper-case
            result = Regex.Replace(result, "(n)([aeiouAEIOU])", "$1y$2"); // Replace 'n' + any of aeiou with "ny" + previous char
            result = Regex.Replace(result, "(N)([aeiouAEIOU])", "$1Y$2"); // Same as above but for upper-case 'N'
            result = result.Replace("ove", "uv"); // Replace "ove" with "uv"

            return Regex.Replace(result, "!{1,3}", _ => RandomFace());
        }

        private string RandomFace() => $" {s_faces[rng.Next(s_faces.Length)]} ";
    }
}
