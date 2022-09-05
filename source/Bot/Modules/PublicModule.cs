using Bot.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Modules
{
    // Modules must be public and inherit from an IModuleBase
    public class GlobalModule : InteractionModuleBase
    {

        private readonly DiceRollService rollService;
        private readonly ChannelCommunicationService channelCommunicationService;
        private readonly InteractionHandlingService interactionHandlingService;

        public GlobalModule(
            DiceRollService rollService,
            ChannelCommunicationService channelCommunicationService,
            InteractionHandlingService interactionHandlingService)
        {
            this.rollService = rollService ?? throw new System.ArgumentNullException(nameof(rollService));
            this.channelCommunicationService = channelCommunicationService;
            this.interactionHandlingService = interactionHandlingService ?? throw new ArgumentNullException(nameof(interactionHandlingService));
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
            var diceExpression = RollDiceAndGetBackString(expression);
            var rerollId = Guid.NewGuid().ToString();
            var buttonBuilder = new ComponentBuilder().WithButton(emote: new Emoji("🎲"), customId: rerollId);

            interactionHandlingService.RegisterCallbackHandler(rerollId, new InteractionButtonCallbackProvider(async smc =>
            {
                var updatedExpression = RollDiceAndGetBackString(expression);
                await smc.UpdateAsync(mp =>
                {
                    mp.Content = updatedExpression;
                    mp.Components = buttonBuilder.Build();
                });
            }));

            await RespondAsync(diceExpression, ephemeral: isPrivate, components: buttonBuilder.Build());
        }

        private string RollDiceAndGetBackString(string expression)
        {
            var expr = DiceRollService.GetDiceExpression(expression);
            var roll = expr.EvaluateWithDetails();
            var total = roll.Values.SelectMany(f => f).Sum();
            var builder = new StringBuilder();
            builder.AppendLine("```");
            builder.AppendLine(expr.ToString());
            builder.AppendLine($"Total: {total}");
            foreach (var value in roll)
            {
                builder.AppendLine($"\t{value.Key}: {string.Join(", ", value.Value)}");
            }
            builder.AppendLine("```");
            return builder.ToString();
        }
    }

}
