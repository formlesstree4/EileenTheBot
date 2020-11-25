using System;
using System.Threading.Tasks;
using Bot.Models;
using Bot.Models.Currency;
using Discord;
using Discord.WebSocket;
using Hangfire;

namespace Bot.Services
{

    public sealed class CurrencyService
    {

        public const byte MaximumLevel = 30;

        private readonly UserService userService;
        private readonly DiscordSocketClient client;
        private readonly StupidTextService stupidTextService;
        private readonly Func<LogMessage, Task> logger;


        public CurrencyService(
            UserService userService,
            DiscordSocketClient client,
            StupidTextService stupidTextService,
            Func<LogMessage, Task> logger)
        {
            this.userService = userService;
            this.client = client;
            this.stupidTextService = stupidTextService;
            this.logger = logger;
        }


        public async Task InitializeService()
        {
            Write($"Initializing and creating background job(s)");
            RecurringJob.AddOrUpdate("currencyUpdate", () => UpdateUserCurrency(), Cron.Hourly);
            RecurringJob.AddOrUpdate("currencyDailyReset", () => ResetDailyClaim(), Cron.Daily);
            Write($"Registering profile service callback");
            userService.RegisterProfileCallback(async (userData, discordInfo) => {
                var embedBuilder = new EmbedBuilder();
                var currencyData = GetOrCreateCurrencyData(userData);
                embedBuilder
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName(discordInfo.Username)
                        .WithIconUrl(discordInfo.GetAvatarUrl() ?? discordInfo.GetDefaultAvatarUrl()))
                    .AddField(new EmbedFieldBuilder()
                        .WithName("Currency")
                        .WithValue($"{currencyData.Currency:N0}/{currencyData.MaxCurrency:N0}")
                        .WithIsInline(true))
                    .AddField(new EmbedFieldBuilder()
                        .WithName("Level")
                        .WithValue($"{currencyData.Level:N0}")
                        .WithIsInline(true))
                    .AddField(new EmbedFieldBuilder()
                        .WithName("Prestige")
                        .WithValue($"{currencyData.Prestige:N0}")
                        .WithIsInline(true))
                    .AddField(new EmbedFieldBuilder()
                        .WithName("Daily Claim")
                        .WithValue(GetDailyClaimLabelValue(currencyData))
                        .WithIsInline(true))
                    .WithColor(new Color(152, 201, 124))
                    .WithCurrentTimestamp()
                    .WithFooter(stupidTextService.GetRandomStupidText())
                    .WithTitle("Currency Details");
                return await Task.FromResult(embedBuilder.Build());
            });
            Write("Initialization has finished");
            await Task.Yield();
        }

        public async Task UpdateUserCurrency()
        {
            Write("Running hourly task of updating user currency...");
            
            // Inside each UserData object is a Tag instance of the CurrencyData.
            // This currency data is solely responsible for holding things like:
            //  1. The User's current 'currency level'
            //  2. The User's current currency value
            //  3. The User's current prestige value
            //  4. The User's soft-cap for currency
            foreach(var userData in userService.WalkUsers())
            {
                var currencyData = GetOrCreateCurrencyData(userData);
                Write($"Performing passive check for {userData.UserId}...", LogSeverity.Verbose);
                if (currencyData.Currency >= currencyData.PassiveCurrencyCap) continue;
                ulong currencyToAdd = CalculatePassiveCurrency(currencyData);
                Write($"The check was passed. Incrementing the currency by {currencyToAdd:N0}");
                currencyData.Currency += currencyToAdd;
            }
            Write("All user currency data has been updated");
            await Task.Yield();
        }

        public async Task ResetDailyClaim()
        {
            Write("Running Daily Task of resetting the daily claim...");
            foreach(var userData in userService.WalkUsers())
            {
                var currencyData = GetOrCreateCurrencyData(userData);
                Write($"Resetting {userData.UserId}...", LogSeverity.Verbose);
                currencyData.DailyClaim = null;
            }
            Write("Daily Task has been concluded");
            await Task.Yield();
        }

        public EileenCurrencyData GetOrCreateCurrencyData(EileenUserData userData) =>
            userData.GetOrAddTagData<EileenCurrencyData>("currencyData", CreateNewCurrencyData);

        public void UpdateCurrencyDataLevels(EileenCurrencyData currencyData)
        {
            currencyData.MaxCurrency = GetCurrencyForLevel(currencyData.Level);
            currencyData.PassiveCurrencyCap = GetPassiveCapForLevel(currencyData.Level);
        }

        public void ProcessDailyClaimOfCurrency(EileenCurrencyData currencyData)
        {
            currencyData.Currency += CalculatePassiveCurrency(currencyData) * 3UL;
            currencyData.DailyClaim = DateTime.Now;
        }


        private string GetDailyClaimLabelValue(EileenCurrencyData currencyData) => currencyData.DailyClaim == null ? "No" : "Yes";
        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(CurrencyService), message));
        }

        private EileenCurrencyData CreateNewCurrencyData()
        {
            Write($"New currency data is being created...");
            // This sets the pace
            return new EileenCurrencyData
            {
                Currency = 0,
                Level = 1,
                MaxCurrency = GetCurrencyForLevel(1),
                PassiveCurrencyCap = GetPassiveCapForLevel(1),
                Prestige = 0
            };

        }

        private ulong GetCurrencyForLevel(byte level) => 100UL + (level * 10UL);

        private ulong GetPassiveCapForLevel(byte level) => (ulong)Math.Ceiling(GetCurrencyForLevel(level) * 0.9);

        private ulong CalculatePassiveCurrency(EileenCurrencyData currencyData) => 1UL * Math.Max(1, (ulong)Math.Ceiling(currencyData.Prestige * 1.5));

    }

}