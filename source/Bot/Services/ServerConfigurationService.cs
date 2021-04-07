using Bot.Models;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hangfire;
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
        private readonly Func<LogMessage, Task> logger;


        public ServerConfigurationService(
            DiscordSocketClient client,
            RavenDatabaseService ravenDatabaseService,
            Func<LogMessage, Task> logger)
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
            Write($"Saving...");
            using (var session = ravenDatabaseService.GetOrAddDocumentStore("erector_core").OpenAsyncSession())
            {
                foreach (var configuration in configurations)
                {
                    Write($"Saving {configuration.Key}...", LogSeverity.Verbose);
                    await session.StoreAsync(
                        entity: configuration.Value,
                        id: configuration.Key.ToString());
                    Write($"Saved {configuration.Key}!", LogSeverity.Verbose);
                }
                Write($"Flushing changes to RavenDB...", LogSeverity.Verbose);
                await session.SaveChangesAsync();
            }
            Write($"Saved!");
        }

        private async Task OnGuildJoined(SocketGuild arg)
        {
            Write($"A new guild has been detected. Creating defaults...");
            await GetOrCreateConfigurationAsync(arg);
            Write($"Guild configuration created.");
        }

        private async Task OnClientConnected()
        {
            Write($"{nameof(OnClientConnected)} has been invoked. Loading configuration for {client.Guilds.Count} guild(s)");
            await LoadServerConfigurations();
            Write($"Configurations have been loaded!");
        }

        private async Task LoadServerConfigurations()
        {
            using (var session = ravenDatabaseService.GetOrAddDocumentStore("erector_core").OpenAsyncSession())
            {
                foreach (var guild in client.Guilds)
                {
                    Write($"Initial load for {guild.Id}", LogSeverity.Verbose);
                    var data = await session.LoadAsync<ServerConfigurationData>(id: guild.Id.ToString());
                    configurations.AddOrUpdate(guild.Id, (guildId) => data, (guildId, original) => data);
                    Write($"Loaded {guild.Id}!", LogSeverity.Verbose);
                }
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
            Write("Fetching Guild Configuration Data...", LogSeverity.Verbose);
            return await Task.FromResult(configurations.GetOrAdd(id, guildId => new ServerConfigurationData
            {
                ServerId = guildId,
                CommandPrefix = ravenDatabaseService.Configuration.CommandPrefix
            }));
        }

        public async Task ReloadAll()
        {
            Write($"Reloading ALL server configurations");
            await LoadServerConfigurations();
            Write($"All configurations loaded successfully");
        }

        public async Task ReloadGuild(ulong guildId)
        {
            using (var session = ravenDatabaseService.GetOrAddDocumentStore("erector_core").OpenAsyncSession())
            {
                var data = await session.LoadAsync<ServerConfigurationData>(id: guildId.ToString());
                configurations.AddOrUpdate(guildId, (guildId) => data, (guildId, original) => data);
            }
        }

        public async Task ReloadGuild(IGuild guild) => await ReloadGuild(guild.Id);

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(ServerConfigurationService), message));
        }

    }

}