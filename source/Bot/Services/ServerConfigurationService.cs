using Bot.Models.Eileen;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.Services
{

    [Summary("Maintains all Guild related information (command prefix, sub-service configuration data, permissions), including detecting when the Bot joins a new Guild and must setup the defaults.")]
    public sealed class ServerConfigurationService : IEileenService
    {
        private readonly DiscordSocketClient _client;
        private readonly ConcurrentDictionary<ulong, ServerConfigurationData> _configurations;
        private readonly ILogger<ServerConfigurationService> _logger;


        public ServerConfigurationService(
            DiscordSocketClient client,
            ILogger<ServerConfigurationService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurations = new ConcurrentDictionary<ulong, ServerConfigurationData>();
            _client.Connected += OnClientConnected;
            _client.Disconnected += OnClientDisconnected;
            _client.JoinedGuild += OnGuildJoined;
            RecurringJob.AddOrUpdate("serverConfigAutoSave", () => SaveServiceAsync(), Cron.Hourly);
        }

        public async Task SaveServiceAsync()
        {
            _logger.LogInformation($"Saving...");

            _logger.LogInformation($"Saved!");
        }

        private async Task OnGuildJoined(SocketGuild arg)
        {
            _logger.LogInformation($"A new guild has been detected. Creating defaults...");
            await GetOrCreateConfigurationAsync(arg);
            _logger.LogInformation($"Guild configuration created.");
        }

        private async Task OnClientConnected()
        {
            _logger.LogInformation("{eventName} has been invoked. Loading configuration for {guilds} guild(s)", nameof(OnClientConnected), _client.Guilds.Count);
            await LoadServerConfigurations();
            _logger.LogInformation($"Configurations have been loaded!");
        }

        private async Task LoadServerConfigurations()
        {
            foreach (var guild in _client.Guilds)
            {
                _logger.LogTrace("Initial load for {guildId}", guild.Id);
                //var data = await session.LoadAsync<ServerConfigurationData>(id: guild.Id.ToString());
                //_configurations.AddOrUpdate(guild.Id, (guildId) => data, (guildId, original) => data);
                _logger.LogTrace("Loaded {guildId}!", guild.Id);
            }
        }

        private async Task OnClientDisconnected(Exception arg)
        {
            await SaveServiceAsync();
        }


        public async Task<ServerConfigurationData> GetOrCreateConfigurationAsync(IGuild guild)
            => await GetOrCreateConfigurationAsync(guild.Id);

        public async Task<ServerConfigurationData> GetOrCreateConfigurationAsync(ulong id)
        {
            _logger.LogTrace("Fetching Guild Configuration Data...");
            return await Task.FromResult(_configurations.GetOrAdd(id, guildId => new ServerConfigurationData
            {
                ServerId = guildId,
                TrustedUsers = new List<ulong>(),
                Enabled = true
            }));
        }

        public async Task ReloadAll()
        {
            _logger.LogInformation($"Reloading ALL server configurations");
            await LoadServerConfigurations();
            _logger.LogInformation($"All configurations loaded successfully");
        }

        public async Task ReloadGuild(ulong guildId)
        {
            //using var session = _ravenDatabaseService.GetOrAddDocumentStore("erector_core").OpenAsyncSession();
            //var data = await session.LoadAsync<ServerConfigurationData>(id: guildId.ToString());
            //_configurations.AddOrUpdate(guildId, (guildId) => data, (guildId, original) => data);
        }

        public async Task ReloadGuild(IGuild guild) => await ReloadGuild(guild.Id);


    }

}
