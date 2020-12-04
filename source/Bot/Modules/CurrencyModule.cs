using System.Threading.Tasks;
using Bot.Services;
using Discord;
using Discord.Commands;

namespace Bot.Modules
{


    public sealed class CurrencyModule : ModuleBase<SocketCommandContext>
    {

        public CurrencyService CurrencyService { get; set; }

        public UserService UserService { get; set; }

        [Command("rankup")]
        [Summary("Spends all of the User's available currency and level's them up to the next rank")]
        public async Task ProcessLevelRequestAsync()
        {
            var userData = await UserService.GetOrCreateUserData(Context.User);
            var currencyData = CurrencyService.GetOrCreateCurrencyData(userData);
            if (currencyData.Level == CurrencyService.MaximumLevel)
            {
                await Context.Channel.SendMessageAsync($"You have reached the maximum level of {CurrencyService.MaximumLevel}. Consider using the prestige command instead.");
            }
            if (currencyData.Currency >= currencyData.MaxCurrency)
            {
                currencyData.Currency -= currencyData.MaxCurrency;
                currencyData.Level += 1;
                CurrencyService.UpdateCurrencyDataLevels(currencyData);
                await Context.Channel.SendMessageAsync($"Congratuations! You've reached Level {currencyData.Level}!");
            }

        }

        [Command("prestige")]
        [Summary("Resets a User's level and currency and increments their prestige number by one")]
        public async Task ProcessPrestigeRequestAsync()
        {
            var userData = await UserService.GetOrCreateUserData(Context.User);
            var currencyData = CurrencyService.GetOrCreateCurrencyData(userData);
            if (currencyData.Level != CurrencyService.MaximumLevel)
            {
                await Context.Channel.SendMessageAsync("You have not yet reached the maximum level for Prestige.");
                return;
            }
            currencyData.Prestige += 1;
            currencyData.Level = 1;
            currencyData.Currency = 0;
            CurrencyService.UpdateCurrencyDataLevels(currencyData);
            await Context.Channel.SendMessageAsync($"You have successfully incremented your Prestige! You are now");
        }


        [Command("dailyc")]
        [Summary("Processes a request to claim a finite amount of currency for this User based on their current level (and prestige)")]
        public async Task ProcessDailyCurrencyAsync()
        {
            var userData = await UserService.GetOrCreateUserData(Context.User);
            var currencyData = CurrencyService.GetOrCreateCurrencyData(userData);
            if (currencyData.DailyClaim != null)
            {
                await Context.Message.AddReactionAsync(new Emoji("👎"));
                return;
            }
            CurrencyService.ProcessDailyClaimOfCurrency(currencyData);
            await Context.Message.AddReactionAsync(new Emoji("👍"));
        }

    }

}