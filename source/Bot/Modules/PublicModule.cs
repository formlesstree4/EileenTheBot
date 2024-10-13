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

        private readonly DiceRollService _rollService;
        private readonly ChannelCommunicationService _channelCommunicationService;
        private readonly InteractionHandlingService _interactionHandlingService;

        public GlobalModule(
            DiceRollService rollService,
            ChannelCommunicationService channelCommunicationService,
            InteractionHandlingService interactionHandlingService)
        {
            _rollService = rollService ?? throw new ArgumentNullException(nameof(rollService));
            _channelCommunicationService = channelCommunicationService;
            _interactionHandlingService = interactionHandlingService ?? throw new ArgumentNullException(nameof(interactionHandlingService));
        }

        [SlashCommand("roll", "Performs a dice roll", runMode: RunMode.Async)]
        public async Task RollAsync(
            [Summary("expression", "The die expression to roll")] string expression,
            [Summary("private", "True or false for the roll being hidden")] bool isPrivate = true)
        {
            var diceExpression = RollDiceAndGetBackString(expression);
            var rerollId = Guid.NewGuid().ToString();
            var buttonBuilder = new ComponentBuilder().WithButton(emote: new Emoji("🎲"), customId: rerollId);

            _interactionHandlingService.RegisterCallbackHandler(rerollId, new InteractionButtonCallbackProvider(async smc =>
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
