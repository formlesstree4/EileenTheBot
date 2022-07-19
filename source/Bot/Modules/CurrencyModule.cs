using Bot.Services;
using Discord.Interactions;
using System.Threading.Tasks;

namespace Bot.Modules
{

    [Group("currency", "Interact with the currency system")]
    public sealed class CurrencyModule : InteractionModuleBase
    {
        private readonly CurrencyService currencyService;
        private readonly UserService userService;

        public CurrencyModule(
            CurrencyService currencyService,
            UserService userService)
        {
            this.currencyService = currencyService ?? throw new System.ArgumentNullException(nameof(currencyService));
            this.userService = userService ?? throw new System.ArgumentNullException(nameof(userService));
        }


        [SlashCommand("rankup", "Spends all of the User's available currency and level's them up to the next rank")]
        public async Task ProcessLevelRequestAsync()
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            var currencyData = currencyService.GetOrCreateCurrencyData(userData);
            if (currencyData.Level == CurrencyService.MaximumLevel)
            {
                await RespondAsync($"You have reached the maximum level of {CurrencyService.MaximumLevel}. Consider using the prestige command instead.");
            }
            if (currencyData.Currency >= currencyData.MaxCurrency)
            {
                currencyData.Currency -= currencyData.MaxCurrency;
                currencyData.Level += 1;
                currencyService.UpdateCurrencyDataLevels(currencyData);
                await RespondAsync($"Congratuations! You've reached Level {currencyData.Level}!");
            }

        }

        [SlashCommand("prestige", "Resets a User's level and currency and increments their prestige number by one")]
        public async Task ProcessPrestigeRequestAsync()
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            var currencyData = currencyService.GetOrCreateCurrencyData(userData);
            if (currencyData.Level != CurrencyService.MaximumLevel)
            {
                await RespondAsync("You have not yet reached the maximum level for Prestige.");
                return;
            }
            currencyData.Prestige += 1;
            currencyData.Level = 1;
            currencyData.Currency = 0;
            currencyService.UpdateCurrencyDataLevels(currencyData);
            await RespondAsync($"You have successfully incremented your Prestige! You are now");
        }


        [SlashCommand("daily", "Performs a daily, finite claim of currency")]
        public async Task ProcessDailyCurrencyAsync()
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            var currencyData = currencyService.GetOrCreateCurrencyData(userData);
            if (currencyData.DailyClaim != null)
            {
                // await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
                await RespondAsync("You have already done your daily claim!", ephemeral: true);
                return;
            }
            currencyService.ProcessDailyClaimOfCurrency(currencyData);
            await RespondAsync("You have claiemd your daily currency amount; check back tomorrow to do so again", ephemeral: true);
        }

    }

}
