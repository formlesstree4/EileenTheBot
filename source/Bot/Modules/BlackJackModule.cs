using Bot.Services.Casino.BlackJack;
using Discord;
using Discord.Interactions;
using System;
using System.Threading.Tasks;

namespace Bot.Modules
{
    [Group("blackjack", "Groups all BlackJack commands together")]
    public sealed class BlackJackModule : InteractionModuleBase
    {
        private readonly BlackJackService blackJackService;
        private readonly BlackJackTableRunnerService blackJackTableRunnerService;

        public BlackJackModule(
            BlackJackService blackJackService,
            BlackJackTableRunnerService blackJackTableRunnerService)
        {
            this.blackJackService = blackJackService ?? throw new ArgumentNullException(nameof(blackJackService));
            this.blackJackTableRunnerService = blackJackTableRunnerService ?? throw new ArgumentNullException(nameof(blackJackTableRunnerService));
        }

        [SlashCommand("begin", "Opens a new BlackJack Table")]
        public async Task CreateTable()
        {
            await blackJackService.CreateNewBlackJackGame(Context.Guild);
            await RespondAsync("A new game table has been created!");
        }

        [SlashCommand("join", "Joins a BlackJack table")]
        public async Task Join()
        {
            if (Context.Channel is IThreadChannel tc)
            {
                var game = blackJackService.FindBlackJackGame(tc);
                await blackJackTableRunnerService.AddPlayerSafelyToTable(game, Context.User);
                await RespondAsync($"Welcome to the table {Context.User.Mention}! Here are a few preset Bid buttons to interact with. Alternately you set your Bid directly with `/blackjack bid <amount>` to set your Bid to any number",
                    ephemeral: true, components: BlackJackTableRunnerService.GetBidButtonComponents(tc.Id).Build());
            }
            else
            {
                await DeferAsync();
            }
        }

        [SlashCommand("leave", "Leaves a BlackJack table")]
        public async Task Leave()
        {
            if (Context.Channel is IThreadChannel tc)
            {
                var game = blackJackService.FindBlackJackGame(tc);
                if (blackJackTableRunnerService.RemovePlayerSafelyFromTable(game, Context.User))
                {
                    await RespondAsync($"You have been removed from the table! Thank you for playing.", ephemeral: true);
                }
                else
                {
                    await RespondAsync("Couldn't remove you from the table. Have you already left?");
                }
            }
            else
            {
                await DeferAsync();
            }

        }

        [SlashCommand("bet", "Gets or sets your static Bet for the room")]
        public async Task Bet(ulong? amount = null)
        {
            if (Context.Channel is IThreadChannel tc)
            {
                var currentTable = blackJackService.FindBlackJackGame(tc);
                if (!currentTable.CanPlayerAlterBet(Context.User.Id))
                {
                    await RespondAsync("Sorry, you can't change your bet right now!", ephemeral: true);
                    return;
                }
                var player = currentTable.FindPlayer(Context.User.Id);
                if (player is null)
                {
                    await RespondAsync("Sorry, I couldn't find you at the table... which is strange. You should report this as a bug to the maintainer", ephemeral: true);
                    return;
                }
                player.CurrentBet = Math.Max(0, amount ?? 0);
                await RespondAsync($"Your bet has now been set to {player.CurrentBet}", ephemeral: true);
            }
        }


    }
}
