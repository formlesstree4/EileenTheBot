using System;
using System.Threading.Tasks;
using Bot.Models;
using Bot.Models.Currency;
using Bot.Services.RavenDB;
using Discord;
using Discord.WebSocket;
using Hangfire;
using Raven.Client.Documents;

namespace Bot.Services
{

    public sealed class CurrencyService
    {
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly UserService userService;
        private readonly DiscordSocketClient client;
        private readonly StupidTextService stupidTextService;
        private readonly Func<LogMessage, Task> logger;


        public CurrencyService(
            RavenDatabaseService rdbs,
            UserService userService,
            DiscordSocketClient client,
            StupidTextService stupidTextService,
            Func<LogMessage, Task> logger)
        {
            this.ravenDatabaseService = rdbs;
            this.userService = userService;
            this.client = client;
            this.stupidTextService = stupidTextService;
            this.logger = logger;
        }


        public async Task InitializeService()
        {
            Write($"Initializing and creating background job(s)");
            RecurringJob.AddOrUpdate("currencyUpdate", () => UpdateUserCurrency(), Cron.Hourly);
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
                    .WithColor(new Color(152, 201, 124))
                    .WithCurrentTimestamp()
                    .WithFooter(stupidTextService.GetRandomStupidText())
                    .WithTitle("Currency");
                return await Task.FromResult(embedBuilder.Build());
            });
            await Task.Yield();
        }

        public async Task UpdateUserCurrency()
        {
            Write("Running hourly task of updating user currency...");
            using (var session = ravenDatabaseService.GetUserConnection.OpenAsyncSession())
            {
                // Inside each UserData object is a Tag instance of the CurrencyData.
                // This currency data is solely responsible for holding things like:
                //  1. The User's current 'currency level'
                //  2. The User's current currency value
                //  3. The User's current prestige value
                //  4. The User's soft-cap for currency
                foreach(var userData in await session.Query<EileenUserData>().ToListAsync())
                {
                    var currencyData = GetOrCreateCurrencyData(userData);
                    Write($"Performing passive check for {userData.UserId}...", LogSeverity.Verbose);
                    if (currencyData.Currency >= currencyData.PassiveCurrencyCap) continue;
                    var currencyToAdd = 1UL * Math.Max(1, (ulong)Math.Ceiling(currencyData.Prestige * 1.5));
                    Write($"The check was passed. Incrementing the currency by {currencyToAdd:N0}");
                    currencyData.Currency += currencyToAdd;
                }
            }
            Write("All user currency data has been updated");
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(CurrencyService), message));
        }

        private EileenCurrencyData GetOrCreateCurrencyData(EileenUserData userData) =>
            userData.GetOrAddTagData<EileenCurrencyData>("currencyData", CreateNewCurrencyData);


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


    }


}