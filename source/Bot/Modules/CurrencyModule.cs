using Bot.Services;
using Discord.Commands;
using System.Threading.Tasks;

namespace Bot.Modules
{


    public sealed class CurrencyModule : ModuleBase<SocketCommandContext>
    {
        private readonly CurrencyService currencyService;
        private readonly UserService userService;
        private readonly ReactionHelperService reactionHelperService;

        public CurrencyModule(
            CurrencyService currencyService,
            UserService userService,
            ReactionHelperService reactionHelperService)
        {
            this.currencyService = currencyService ?? throw new System.ArgumentNullException(nameof(currencyService));
            this.userService = userService ?? throw new System.ArgumentNullException(nameof(userService));
            this.reactionHelperService = reactionHelperService ?? throw new System.ArgumentNullException(nameof(reactionHelperService));
        }


        [Command("rankup")]
        [Summary("Spends all of the User's available currency and level's them up to the next rank")]
        public async Task ProcessLevelRequestAsync()
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            var currencyData = currencyService.GetOrCreateCurrencyData(userData);
            if (currencyData.Level == CurrencyService.MaximumLevel)
            {
                await ReplyAsync($"You have reached the maximum level of {CurrencyService.MaximumLevel}. Consider using the prestige command instead.");
            }
            if (currencyData.Currency >= currencyData.MaxCurrency)
            {
                currencyData.Currency -= currencyData.MaxCurrency;
                currencyData.Level += 1;
                currencyService.UpdateCurrencyDataLevels(currencyData);
                await ReplyAsync($"Congratuations! You've reached Level {currencyData.Level}!");
            }

        }

        [Command("prestige")]
        [Summary("Resets a User's level and currency and increments their prestige number by one")]
        public async Task ProcessPrestigeRequestAsync()
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            var currencyData = currencyService.GetOrCreateCurrencyData(userData);
            if (currencyData.Level != CurrencyService.MaximumLevel)
            {
                await ReplyAsync("You have not yet reached the maximum level for Prestige.");
                return;
            }
            currencyData.Prestige += 1;
            currencyData.Level = 1;
            currencyData.Currency = 0;
            currencyService.UpdateCurrencyDataLevels(currencyData);
            await ReplyAsync($"You have successfully incremented your Prestige! You are now");
        }


        [Command("dailyc")]
        [Summary("Processes a request to claim a finite amount of currency for this User based on their current level (and prestige)")]
        public async Task ProcessDailyCurrencyAsync()
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            var currencyData = currencyService.GetOrCreateCurrencyData(userData);
            if (currencyData.DailyClaim != null)
            {
                await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
                return;
            }
            currencyService.ProcessDailyClaimOfCurrency(currencyData);
            await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
        }

    }

}
