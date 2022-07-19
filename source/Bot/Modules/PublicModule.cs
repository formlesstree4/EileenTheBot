using Bot.Services;
using Discord.Interactions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Modules
{
    // Modules must be public and inherit from an IModuleBase
    public class GlobalModule : InteractionModuleBase
    {

        private readonly DiceRollService rollService;

        public GlobalModule(
            DiceRollService rollService)
        {
            this.rollService = rollService ?? throw new System.ArgumentNullException(nameof(rollService));
        }

//#pragma warning disable CS1998
//        [Command("poem")]
//        [Summary("Generates a poem with an optional title")]
//        public async Task PoetryAsync(
//            [Name("Title"),
//            Summary("The optional title of the poem"),
//            Remainder]string title = "")
//        {
//            var config = await serverConfigurationService.GetOrCreateConfigurationAsync(Context.Guild);
//            if (config.ResponderType != Models.ServerConfigurationData.AutomatedResponseType.GPT) return;
//            var url = ravenDatabaseService.Configuration.GptUrl;
//            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out var t)) return;
//            url = url.Replace(t.Port.ToString(), "8081");
//#pragma warning disable CS4014
//            Task.Factory.StartNew(async () =>
//            {
//                using (Context.Channel.EnterTypingState())
//                {
//                    var payload = new { title = (title ?? "") };
//                    var responseType = new { text = "" };
//                    var message = JsonConvert.SerializeObject(payload);
//                    var content = new StringContent(message);
//                    var jsonResponse = await client.PostAsync(url, content);
//                    var poetry = JsonConvert.DeserializeAnonymousType(await jsonResponse.Content.ReadAsStringAsync(), responseType);
//                    var responseString = $"```\r\n{poetry.text}\r\n```";
//                    await Context.Channel.SendMessageAsync(responseString);
//                }
//            });
//#pragma warning restore CS4014
//        }
//#pragma warning restore CS1998


        [SlashCommand("roll", "Performs a dice roll", runMode: RunMode.Async)]
        public async Task RollAsync(
            [Summary("expression", "The die expression to roll")] string expression,
            [Summary("private", "True or false for the roll being hidden")] bool isPrivate = true)
        {
            var expr = rollService.GetDiceExpression(expression);
            var roll = expr.EvaluateWithDetails();
            var total = roll.Values.SelectMany(f => f).Sum();
            var builder = new StringBuilder();
            builder.AppendLine("```");
            builder.AppendLine($"Total: {total}");
            foreach(var value in roll)
            {
                builder.AppendLine($"\t{value.Key}: {string.Join(", ", value.Value)}");
            }
            builder.AppendLine("```");
            await RespondAsync(builder.ToString(), ephemeral: isPrivate);
        }

    }

}
