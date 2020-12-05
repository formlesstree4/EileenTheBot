using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.Models;
using Bot.Services.RavenDB;
using Discord;
using Discord.WebSocket;
using Hangfire;

namespace Bot.Services
{

    public sealed class ServerConfigurationService
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
            using(var session = ravenDatabaseService.GetOrAddDocumentStore("erector_core").OpenAsyncSession())
            {
                foreach(var configuration in configurations)
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
            await GetOrCreateConfigurationAsync(arg);
        }

        private async Task OnClientConnected()
        {
            Write($"{nameof(OnClientConnected)} has been invoked. Loading configuration for {client.Guilds.Count} guild(s)");
            foreach(var guild in client.Guilds)
            {
                Write($"Initial load for {guild.Id}", LogSeverity.Verbose);
                await GetOrCreateConfigurationAsync(guild);
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
            Write("Fetching Guild Configuration Data...");
            return await Task.FromResult(configurations.GetOrAdd(id, guildId => new ServerConfigurationData
            {
                ServerId = guildId,
                CommandPrefix = ravenDatabaseService.Configuration.CommandPrefix
            }));
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(ServerConfigurationService), message));
        }

    }

}