using Bot.Models.CommandPermissions;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.Services
{


    public sealed class CommandPermissionsService : IEileenService
    {
        private readonly DiscordSocketClient client;
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly ILogger<CommandPermissionsService> logger;

        public CommandPermissionsService(
            DiscordSocketClient client,
            ServerConfigurationService serverConfigurationService,
            ILogger<CommandPermissionsService> logger)
        {
            this.client = client ??
                throw new ArgumentNullException(nameof(client));
            this.serverConfigurationService = serverConfigurationService ??
                throw new ArgumentNullException(nameof(serverConfigurationService));
            this.logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }


        public async Task<PermissionsEntry> GetOrCreatePermissionsAsync(IGuild guild)
            => await GetOrCreatePermissionsAsync(guild.Id);

        public async Task<PermissionsEntry> GetOrCreatePermissionsAsync(ulong serverId)
        {
            logger.LogTrace("Retrieving permissions for {serverId}", serverId);
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(serverId);
            return configuration.GetOrAddTagData("permissions", () => CreatePermissions(serverId));
        }


        private PermissionsEntry CreatePermissions(ulong serverId)
        {
            return new PermissionsEntry
            {
                Permissions = new List<CommandEntry>()
            };
        }

    }


}
