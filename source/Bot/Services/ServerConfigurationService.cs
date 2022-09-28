using Bot.Models.Eileen;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Bot.Services
{

    [Summary("Maintains all Guild related information (command prefix, sub-service configuration data, permissions), including detecting when the Bot joins a new Guild and must setup the defaults.")]
    public sealed class ServerConfigurationService : IEileenService
    {
        private readonly DiscordSocketClient client;
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly ConcurrentDictionary<ulong, ServerConfigurationData> configurations;
        private readonly ILogger<ServerConfigurationService> logger;


        public ServerConfigurationService(
            DiscordSocketClient client,
            RavenDatabaseService ravenDatabaseService,
            ILogger<ServerConfigurationService> logger)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.ravenDatabaseService = ravenDatabaseService ?? throw new ArgumentNullException(nameof(ravenDatabaseService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.configurations = new ConcurrentDictionary<ulong, ServerConfigurationData>();
            this.client.Connected += OnClientConnected;
            this.client.Disconnected += OnClientDisconnected;
            this.client.JoinedGuild += OnGuildJoined;
            RecurringJob.AddOrUpdate("serverConfigAutoSave", () => SaveServiceAsync(), Cron.Hourly);
        }

        public async Task SaveServiceAsync()
        {
            logger.LogInformation($"Saving...");
            using (var session = ravenDatabaseService.GetOrAddDocumentStore("erector_core").OpenAsyncSession())
            {
                foreach (var configuration in configurations)
                {
                    logger.LogTrace("Saving {configuration}...", configuration.Key);
                    await session.StoreAsync(
                        entity: configuration.Value,
                        id: configuration.Key.ToString());
                    logger.LogTrace("Saved {configuration}!", configuration.Key);
                }
                logger.LogTrace($"Flushing changes to RavenDB...");
                await session.SaveChangesAsync();
            }
            logger.LogInformation($"Saved!");
        }

        private async Task OnGuildJoined(SocketGuild arg)
        {
            logger.LogInformation($"A new guild has been detected. Creating defaults...");
            await GetOrCreateConfigurationAsync(arg);
            logger.LogInformation($"Guild configuration created.");
        }

        private async Task OnClientConnected()
        {
            logger.LogInformation("{eventName} has been invoked. Loading configuration for {guilds} guild(s)", nameof(OnClientConnected), client.Guilds.Count);
            await LoadServerConfigurations();
            logger.LogInformation($"Configurations have been loaded!");
        }

        private async Task LoadServerConfigurations()
        {
            using var session = ravenDatabaseService.GetOrAddDocumentStore("erector_core").OpenAsyncSession();
            foreach (var guild in client.Guilds)
            {
                logger.LogTrace("Initial load for {guildId}", guild.Id);
                var data = await session.LoadAsync<ServerConfigurationData>(id: guild.Id.ToString());
                configurations.AddOrUpdate(guild.Id, (guildId) => data, (guildId, original) => data);
                logger.LogTrace("Loaded {guildId}!", guild.Id);
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
            logger.LogTrace("Fetching Guild Configuration Data...");
            return await Task.FromResult(configurations.GetOrAdd(id, guildId => new ServerConfigurationData
            {
                ServerId = guildId,
                CommandPrefix = ravenDatabaseService.Configuration.CommandPrefix
            }));
        }

        public async Task ReloadAll()
        {
            logger.LogInformation($"Reloading ALL server configurations");
            await LoadServerConfigurations();
            logger.LogInformation($"All configurations loaded successfully");
        }

        public async Task ReloadGuild(ulong guildId)
        {
            using var session = ravenDatabaseService.GetOrAddDocumentStore("erector_core").OpenAsyncSession();
            var data = await session.LoadAsync<ServerConfigurationData>(id: guildId.ToString());
            configurations.AddOrUpdate(guildId, (guildId) => data, (guildId, original) => data);
        }

        public async Task ReloadGuild(IGuild guild) => await ReloadGuild(guild.Id);


    }

}
