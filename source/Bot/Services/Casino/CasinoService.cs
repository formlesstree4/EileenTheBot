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
        protected internal readonly ILogger<CasinoService<THand, TPlayer, TTable, TTableDetails, TServerDetails, TService>> Logger;
        private readonly TService _tableRunnerService;
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly ServerConfigurationService _serverConfigurationService;
        private readonly Dictionary<ulong, TServerDetails> _details = new();

        protected abstract string ServiceName { get; }

        protected CasinoService(
            ILogger<CasinoService<THand, TPlayer, TTable, TTableDetails, TServerDetails, TService>> logger,
            TService tableRunnerService,
            DiscordSocketClient discordSocketClient,
            ServerConfigurationService serverConfigurationService)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _discordSocketClient = discordSocketClient ?? throw new ArgumentNullException(nameof(discordSocketClient));
            _serverConfigurationService = serverConfigurationService ?? throw new ArgumentNullException(nameof(serverConfigurationService));
            _tableRunnerService = tableRunnerService;
            _discordSocketClient.Ready += HandleClientIsReady;
            _discordSocketClient.ThreadDeleted += HandleThreadDeleted;
        }

        private async Task HandleClientIsReady()
        {
            foreach (var guild in _discordSocketClient.Guilds)
            {
                var serverDetails = await _serverConfigurationService.GetOrCreateConfigurationAsync(guild);
                var details = serverDetails.GetOrAddTagData(ServiceName, () => CreateDefaultDetails());
                _details.Add(guild.Id, details);
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
            _tableRunnerService.StopAndRemoveTable(threadId);
            return Task.CompletedTask;
        }

        protected abstract string GetNextTableName();



        public abstract TServerDetails CreateDefaultDetails();

        public virtual async Task<TTable> CreateNewGame(IGuild guild, ulong? threadId = null)
        {
            var tableChannelId = _details[guild.Id].ChannelId;
            if (tableChannelId is null) return null; // fuck it I don't care
            var tableChannel = await _discordSocketClient.GetChannelAsync((ulong)tableChannelId) as ITextChannel;
            IThreadChannel thread;
            if (threadId is null)
            {
                thread = await tableChannel.CreateThreadAsync(GetNextTableName());
            }
            else
            {
                thread = await _discordSocketClient.GetChannelAsync(threadId.Value) as IThreadChannel;
            }
            var table = _tableRunnerService.GetOrCreateTable(thread);
            _tableRunnerService.StartTableForChannel(thread);
            return table;
        }

        public virtual void SetGameChannel(IGuild guild, IChannel channel)
        {
            if (_details.TryGetValue(guild.Id, out var serverDetails))
            {
                serverDetails.ChannelId = channel.Id;
            }
        }

        public virtual TTable FindGame(IThreadChannel thread)
        {
            return _tableRunnerService.GetOrCreateTable(thread);
        }

        public virtual async Task SaveServiceAsync()
        {
            foreach (var bjd in _details)
            {
                var serverDetails = await _serverConfigurationService.GetOrCreateConfigurationAsync(bjd.Key);
                serverDetails.SetTagData(ServiceName, bjd.Value);
            }
        }

    }
}
