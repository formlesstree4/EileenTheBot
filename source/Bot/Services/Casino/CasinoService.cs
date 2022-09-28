using Bot.Models.Casino;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.Services.Casino
{
    public abstract class CasinoService<THand, TPlayer, TTable, TTableDetails, TServerDetails, TService> : IEileenService
        where TServerDetails : CasinoServerDetails, new()
        where THand : CasinoHand
        where TPlayer : CasinoPlayer<THand>
        where TTable : CasinoTable<TPlayer, THand>
        where TTableDetails : CasinoTableDetails<TTable, TPlayer, THand>
        where TService : TableRunnerService<THand, TPlayer, TTable, TTableDetails>
    {
        protected internal readonly ILogger<CasinoService<THand, TPlayer, TTable, TTableDetails, TServerDetails, TService>> logger;
        protected internal readonly TService tableRunnerService;
        protected internal readonly DiscordSocketClient discordSocketClient;
        protected internal readonly ServerConfigurationService serverConfigurationService;
        protected internal readonly Dictionary<ulong, TServerDetails> details = new();

        protected abstract string ServiceName { get; }

        protected CasinoService(
            ILogger<CasinoService<THand, TPlayer, TTable, TTableDetails, TServerDetails, TService>> logger,
            TService tableRunnerService,
            DiscordSocketClient discordSocketClient,
            ServerConfigurationService serverConfigurationService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.discordSocketClient = discordSocketClient ?? throw new ArgumentNullException(nameof(discordSocketClient));
            this.serverConfigurationService = serverConfigurationService ?? throw new ArgumentNullException(nameof(serverConfigurationService));
            this.tableRunnerService = tableRunnerService;
            this.discordSocketClient.Ready += HandleClientIsReady;
            this.discordSocketClient.ThreadDeleted += HandleThreadDeleted;
        }

        private async Task HandleClientIsReady()
        {
            foreach (var guild in discordSocketClient.Guilds)
            {
                var serverDetails = await serverConfigurationService.GetOrCreateConfigurationAsync(guild);
                var details = serverDetails.GetOrAddTagData(ServiceName, () => CreateDefaultDetails());
                this.details.Add(guild.Id, details);
                if (details.ChannelId == null) continue;
                foreach (var thread in guild.ThreadChannels)
                {
                    if (thread.ParentChannel.Id != details.ChannelId) continue;
                    await CreateNewGame(guild, thread.Id);
                }
            }
        }

        private Task HandleThreadDeleted(Cacheable<SocketThreadChannel, ulong> arg)
        {
            var threadId = arg.Id;
            tableRunnerService.StopAndRemoveTable(threadId);
            return Task.CompletedTask;
        }

        protected abstract string GetNextTableName();



        public abstract TServerDetails CreateDefaultDetails();

        public virtual async Task<TTable> CreateNewGame(IGuild guild, ulong? threadId = null)
        {
            var tableChannelId = details[guild.Id].ChannelId;
            if (tableChannelId is null) return null; // fuck it I don't care
            var tableChannel = await discordSocketClient.GetChannelAsync((ulong)tableChannelId) as ITextChannel;
            IThreadChannel thread;
            if (threadId is null)
            {
                thread = await tableChannel.CreateThreadAsync(GetNextTableName());
            }
            else
            {
                thread = await discordSocketClient.GetChannelAsync(threadId.Value) as IThreadChannel;
            }
            var table = tableRunnerService.GetOrCreateTable(thread);
            tableRunnerService.StartTableForChannel(thread);
            return table;
        }

        public virtual void SetGameChannel(IGuild guild, IChannel channel)
        {
            if (details.TryGetValue(guild.Id, out var serverDetails))
            {
                serverDetails.ChannelId = channel.Id;
            }
        }

        public virtual TTable FindGame(IThreadChannel thread)
        {
            return tableRunnerService.GetOrCreateTable(thread);
        }

        public virtual async Task SaveServiceAsync()
        {
            foreach (var bjd in details)
            {
                var serverDetails = await serverConfigurationService.GetOrCreateConfigurationAsync(bjd.Key);
                serverDetails.SetTagData(ServiceName, bjd.Value);
            }
        }

    }
}
