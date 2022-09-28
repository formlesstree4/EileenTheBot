using Bot.Models.Casino.BlackJack;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.Services.Casino.BlackJack
{

    /// <summary>
    ///     Servces as a buffer, of sorts, between Discord and <see cref="BlackJackTableRunnerService"/>
    /// </summary>
    public sealed class BlackJackService : CasinoService<BlackJackHand, BlackJackPlayer, BlackJackTable, BlackJackTableDetails, BlackJackServerDetails, BlackJackTableRunnerService>
    {
        public BlackJackService(
            ILogger<CasinoService<BlackJackHand, BlackJackPlayer, BlackJackTable, BlackJackTableDetails, BlackJackServerDetails, BlackJackTableRunnerService>> logger,
            BlackJackTableRunnerService tableRunnerService,
            DiscordSocketClient discordSocketClient,
            ServerConfigurationService serverConfigurationService) : base(logger, tableRunnerService, discordSocketClient, serverConfigurationService)
        { }

        protected override string ServiceName => nameof(BlackJackService);

        protected override string GetNextTableName() => "BlackJack Table";

        public override BlackJackServerDetails CreateDefaultDetails() => new();

    }

}
