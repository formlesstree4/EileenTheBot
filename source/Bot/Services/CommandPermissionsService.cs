using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.Models.CommandPermissions;
using Discord;
using Discord.WebSocket;

namespace Bot.Services
{


    public sealed class CommandPermissionsService
    {
        private readonly DiscordSocketClient client;
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly Func<LogMessage, Task> logger;

        public CommandPermissionsService(
            DiscordSocketClient client,
            ServerConfigurationService serverConfigurationService,
            Func<LogMessage, Task> logger)
        {
            this.client = client ??
                throw new System.ArgumentNullException(nameof(client));
            this.serverConfigurationService = serverConfigurationService ??
                throw new ArgumentNullException(nameof(serverConfigurationService));
            this.logger = logger ??
                throw new System.ArgumentNullException(nameof(logger));
        }


        public async Task<PermissionsEntry> GetOrCreatePermissionsAsync(ulong serverId)
        {
            Write($"Retreiving permissions for {serverId}...");
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(serverId);
            return configuration.GetOrAddTagData<PermissionsEntry>("permissions", () => CreatePermissions(serverId));
        }


        private PermissionsEntry CreatePermissions(ulong serverId)
        {
            return new PermissionsEntry
            {
                Permissions = new List<CommandEntry>()
            };
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(CommandPermissionsService), message));
        }

    }


}