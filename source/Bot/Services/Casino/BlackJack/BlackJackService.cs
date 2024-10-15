using Bot.Models.Casino.BlackJack;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Bot.Services.Casino.BlackJack
{

    /// <summary>
    /// Serves as a buffer, of sorts, between Discord and <see cref="BlackJackTableRunnerService"/>
    /// </summary>
    public sealed class BlackJackService : CasinoService<BlackJackHand, BlackJackPlayer, BlackJackTable, BlackJackTableDetails, BlackJackServerDetails, BlackJackTableRunnerService>
    {
        public BlackJackService(
            ILogger<CasinoService<BlackJackHand, BlackJackPlayer, BlackJackTable, BlackJackTableDetails, BlackJackServerDetails, BlackJackTableRunnerService>> logger,
            BlackJackTableRunnerService tableRunnerService,
            DiscordSocketClient discordSocketClient) : base(logger, tableRunnerService, discordSocketClient)
        { }

        protected override string ServiceName => nameof(BlackJackService);

        protected override string GetNextTableName() => "BlackJack Table";

        public override BlackJackServerDetails CreateDefaultDetails() => new();

    }

}
