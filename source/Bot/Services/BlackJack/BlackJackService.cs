using Bot.Models.BlackJack;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Services.BlackJack
{

    /// <summary>
    ///     Servces as a buffer, of sorts, between Discord and <see cref="BlackJackTableRunnerService"/>
    /// </summary>
    public sealed class BlackJackService : IEileenService
    {
        private readonly BlackJackTableRunnerService blackJackTableRunnerService;
        private readonly DiscordSocketClient discordSocketClient;
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly ILogger<BlackJackService> logger;
        public Dictionary<ulong, BlackJackServerDetails> blackJackDetails = new();



        public BlackJackService(
            BlackJackTableRunnerService blackJackTableRunnerService,
            DiscordSocketClient discordSocketClient,
            ServerConfigurationService serverConfigurationService,
            ILogger<BlackJackService> logger)
        {
            this.blackJackTableRunnerService = blackJackTableRunnerService ?? throw new ArgumentNullException(nameof(blackJackTableRunnerService));
            this.discordSocketClient = discordSocketClient ?? throw new ArgumentNullException(nameof(discordSocketClient));
            this.serverConfigurationService = serverConfigurationService ?? throw new ArgumentNullException(nameof(serverConfigurationService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.discordSocketClient.Ready += HandleClientIsReady;
            this.discordSocketClient.ThreadDeleted += HandleThreadDeleted;
        }

        private Task HandleThreadDeleted(Cacheable<SocketThreadChannel, ulong> arg)
        {
            var threadId = arg.Id;
            
            return Task.CompletedTask;
        }

        private async Task HandleClientIsReady()
        {
            foreach (var guild in discordSocketClient.Guilds)
            {
                var serverDetails = await serverConfigurationService.GetOrCreateConfigurationAsync(guild);
                var details = serverDetails.GetOrAddTagData(nameof(BlackJackService), () => new BlackJackServerDetails());
                blackJackDetails.Add(guild.Id, details);
                if (details.ChannelId == null) continue;
                foreach (var thread in guild.ThreadChannels)
                {
                    if (thread.ParentChannel.Id != details.ChannelId) continue;

                }
            }
        }

        public async Task SaveServiceAsync()
        {
            foreach (var bjd in blackJackDetails)
            {
                var serverDetails = await serverConfigurationService.GetOrCreateConfigurationAsync(bjd.Key);
                serverDetails.SetTagData(nameof(BlackJackService), bjd.Value);
            }
        }

        public async Task<BlackJackTable> CreateNewBlackJackGame(IGuild guild, ulong? threadId = null)
        {
            
        }

        public void SetBlackJackChannel(IGuild guild, IChannel channel)
        {
            if (blackJackDetails.TryGetValue(guild.Id, out BlackJackServerDetails serverDetails))
            {
                serverDetails.ChannelId = channel.Id;
            }
        }

        public BlackJackTable FindBlackJackGame(IThreadChannel thread)
        {

        }

    }

}
