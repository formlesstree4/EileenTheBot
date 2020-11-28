using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Services.RavenDB;
using Discord;
using Discord.WebSocket;

namespace Bot.Services
{


    public sealed class CommandPermissionsService
    {
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly DiscordSocketClient client;
        private readonly ConcurrentDictionary<ulong, Models.CommandPermissions.PermissionsEntry> permissions;
        private readonly Func<LogMessage, Task> logger;

        public CommandPermissionsService(
            RavenDB.RavenDatabaseService ravenDatabaseService,
            DiscordSocketClient client,
            Func<LogMessage, Task> logger)
        {
            this.ravenDatabaseService = ravenDatabaseService ??
                throw new System.ArgumentNullException(nameof(ravenDatabaseService));
            this.client = client ??
                throw new System.ArgumentNullException(nameof(client));
            this.logger = logger ??
                throw new System.ArgumentNullException(nameof(logger));
            permissions = new ConcurrentDictionary<ulong, Models.CommandPermissions.PermissionsEntry>();
            client.Connected += OnClientConnected;
        }

        public async Task InitializeService()
        {
            Write($"Initialization Completed");
            await Task.Yield();
        }

        public async Task SaveServiceAsync()
        {
            Write("Saving Changes...");
            using (var session = ravenDatabaseService.GetCommandPermissionsConnection.OpenAsyncSession())
            {
                foreach (var entry in permissions)
                {
                    await session.StoreAsync(entry.Value, entry.Key.ToString());
                }
                await session.SaveChangesAsync();
            }
            Write("Save completed!");
        }

        private async Task OnClientConnected()
        {
            Write($"{nameof(OnClientConnected)} has been invoked. Loading permissions for {client.Guilds.Count} guild(s)");
            foreach(var guild in client.Guilds)
            {
                Write($"Initial permissions load for {guild.Id}", LogSeverity.Verbose);
                await GetOrCreatePermissionsAsync(guild.Id);
            }
        }

        public async Task<Models.CommandPermissions.PermissionsEntry> GetOrCreatePermissionsAsync(ulong serverId)
        {
            Write($"Retreiving permissions for {serverId}...");
            return permissions.GetOrAdd(serverId, 
                await GetPermissionsAsync(serverId) ??
                await CreatePermissionsAsync(serverId));
        }


        private async Task<Models.CommandPermissions.PermissionsEntry> GetPermissionsAsync(ulong serverId)
        {
            using (var session = ravenDatabaseService.GetCommandPermissionsConnection.OpenAsyncSession())
            {
                Write($"Attempting to search for permissions for {serverId}");
                return await session.LoadAsync<Models.CommandPermissions.PermissionsEntry>(serverId.ToString());
            }
        }

        private async Task<Models.CommandPermissions.PermissionsEntry> CreatePermissionsAsync(ulong serverId)
        {
            var newEntry = new Models.CommandPermissions.PermissionsEntry
            {
                Permissions = new List<Models.CommandPermissions.CommandEntry>()
            };
            using (var session = ravenDatabaseService.GetCommandPermissionsConnection.OpenAsyncSession())
            {
                Write($"Creating permissions for {serverId}");
                await session.StoreAsync(entity: newEntry, id: serverId.ToString());
                await session.SaveChangesAsync();
            }
            return newEntry;
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(CommandPermissionsService), message));
        }

    }


}