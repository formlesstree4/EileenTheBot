using Bot.Models.Eileen;
using Bot.Models.Eileen.Currency;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Bot.Services
{

    [Summary("Helps manage a User's 'Eileen' currency")]
    public sealed class CurrencyService : IEileenService
    {

        public const byte MaximumLevel = 30;

        private readonly UserService _userService;
        private readonly DiscordSocketClient _client;
        private readonly StupidTextService _stupidTextService;
        private readonly ILogger<CurrencyService> _logger;


        public CurrencyService(
            UserService userService,
            DiscordSocketClient client,
            StupidTextService stupidTextService,
            ILogger<CurrencyService> logger)
        {
            _userService = userService;
            _client = client;
            _stupidTextService = stupidTextService;
            _logger = logger;
        }


        public async Task InitializeService()
        {
            _logger.LogInformation($"Initializing and creating background job(s)");
            RecurringJob.AddOrUpdate("currencyUpdate", () => UpdateUserCurrency(), Cron.Hourly);
            RecurringJob.AddOrUpdate("currencyDailyReset", () => ResetDailyClaim(), Cron.Daily);
            _logger.LogInformation($"Registering profile service callback");
            _userService.RegisterProfileCallback(async (embedDetails) =>
            {
                var currencyData = GetOrCreateCurrencyData(embedDetails.UserData);
                embedDetails.PageBuilder
                    .AddField(new EmbedFieldBuilder()
                        .WithName("Amount")
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
                    .WithTitle("Currency Overview");
                return await Task.FromResult(embedDetails);
            });
            _logger.LogInformation("Initialization has finished");
            await Task.Yield();
        }

        public async Task UpdateUserCurrency()
        {
            _logger.LogInformation("Running hourly task of updating user currency...");

            // Inside each UserData object is a Tag instance of the CurrencyData.
            // This currency data is solely responsible for holding things like:
            //  1. The User's current 'currency level'
            //  2. The User's current currency value
            //  3. The User's current prestige value
            //  4. The User's soft-cap for currency
            foreach (var userData in _userService.WalkUsers())
            {
                var currencyData = GetOrCreateCurrencyData(userData);
                _logger.LogTrace("Performing passive check for {userId}...", userData.UserId);
                if (currencyData.Currency >= currencyData.PassiveCurrencyCap) continue;
                ulong currencyToAdd = CalculatePassiveCurrency(currencyData);
                _logger.LogTrace("The check was passed. Incrementing the currency by {currencyToAdd}", currencyToAdd.ToString("N0"));
                currencyData.Currency += currencyToAdd;
            }
            _logger.LogInformation("All user currency data has been updated");
            await Task.Yield();
        }

        public async Task ResetDailyClaim()
        {
            _logger.LogInformation("Running Daily Task of resetting the daily claim...");
            foreach (var userData in _userService.WalkUsers())
            {
                var currencyData = GetOrCreateCurrencyData(userData);
                _logger.LogTrace("Resetting {userId}...", userData.UserId);
                currencyData.DailyClaim = null;
            }
            _logger.LogTrace("Daily Task has been concluded");
            await Task.Yield();
        }

        public EileenCurrencyData GetOrCreateCurrencyData(EileenUserData userData) =>
            userData.GetOrAddTagData("currencyData", CreateNewCurrencyData);

        public static void UpdateCurrencyDataLevels(EileenCurrencyData currencyData)
        {
            currencyData.MaxCurrency = GetCurrencyForLevel(currencyData.Level);
            currencyData.PassiveCurrencyCap = GetPassiveCapForLevel(currencyData.Level);
        }

        public static void ProcessDailyClaimOfCurrency(EileenCurrencyData currencyData)
        {
            currencyData.Currency += CalculatePassiveCurrency(currencyData) * 3UL;
            currencyData.DailyClaim = DateTime.Now;
        }


        private static string GetDailyClaimLabelValue(EileenCurrencyData currencyData) => currencyData.DailyClaim == null ? "No" : "Yes";

        private EileenCurrencyData CreateNewCurrencyData()
        {
            _logger.LogTrace($"New currency data is being created...");
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

        private static ulong GetCurrencyForLevel(byte level) => 100UL + (level * 10UL);

        private static ulong GetPassiveCapForLevel(byte level) => (ulong)Math.Ceiling(GetCurrencyForLevel(level) * 0.9);

        private static ulong CalculatePassiveCurrency(EileenCurrencyData currencyData) => 1UL * Math.Max(1, (ulong)Math.Ceiling(currencyData.Prestige * 1.5));

    }

}
