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
            blackJackTableRunnerService.StopAndRemoveBlackJackTable(threadId);
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
                    await CreateNewBlackJackGame(guild, thread.Id);
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
            var bjChannel = blackJackDetails[guild.Id].ChannelId;
            if (bjChannel is null) return null; // fuck it I don't care
            var channel = await discordSocketClient.GetChannelAsync((ulong)bjChannel) as ITextChannel;
            if (threadId is null)
            {
                var thread = await channel.CreateThreadAsync("BlackJack Table");
                var table = blackJackTableRunnerService.GetOrCreateBlackJackTable(thread);
                blackJackTableRunnerService.StartBlackJackTableForChannel(thread);
                return table;
            }
            else
            {
                var thread = await discordSocketClient.GetChannelAsync(threadId.Value) as IThreadChannel;
                var table = blackJackTableRunnerService.GetOrCreateBlackJackTable(thread);
                blackJackTableRunnerService.StartBlackJackTableForChannel(thread);
                return table;
            }
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
            return blackJackTableRunnerService.GetOrCreateBlackJackTable(thread);
        }

    }

}
