using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models.Macros;
using Discord;
using Microsoft.Extensions.Logging;

namespace Bot.Services
{
    public sealed class MacroService : IEileenService
    {

        private const string MacroTag = "serverMacros";

        private readonly ServerConfigurationService serverConfigurationService;
        private readonly ILogger<MacroService> logger;

        public MacroService(
            ServerConfigurationService serverConfigurationService,
            ILogger<MacroService> logger)
        {
            this.serverConfigurationService = serverConfigurationService ?? throw new ArgumentNullException(nameof(serverConfigurationService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }



        public async Task<(bool, MacroEntry)> TryGetMacroAsync(IGuild guild, string macro) =>
            await TryGetMacroAsync(guild.Id, macro);

        public async Task<(bool, MacroEntry)> TryGetMacroAsync(ulong guildId, string macro)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(guildId);
            var macroTags = configuration.GetOrAddTagData(MacroTag, () => new MacroServerEntries());
            var macroTag = macroTags.Entries.FirstOrDefault(c => c.Macro.Equals(macro, StringComparison.OrdinalIgnoreCase));
            return (macroTag != null, macroTag);
        }

        public async Task AddNewMacroAsync(IGuild guild, MacroEntry entry) =>
            await AddNewMacroAsync(guild.Id, entry);

        public async Task AddNewMacroAsync(ulong guildId, MacroEntry entry)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(guildId);
            var macroTags = configuration.GetOrAddTagData(MacroTag, () => new MacroServerEntries());
            if (macroTags.Entries.FirstOrDefault(c => c.Macro.Equals(entry.Macro, StringComparison.OrdinalIgnoreCase)) != null)
            {
                return;
            }
            macroTags.Entries.Add(entry);
            configuration.SetTagData(MacroTag, macroTags);
        }

        public async Task RemoveMacroAsync(IGuild guild, string macro) =>
            await RemoveMacroAsync(guild.Id, macro);

        public async Task RemoveMacroAsync(ulong guildId, string macro)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(guildId);
            var macroTags = configuration.GetOrAddTagData(MacroTag, () => new MacroServerEntries());
            var tagToRemove = macroTags.Entries.FirstOrDefault(c => c.Macro.Equals(macro, StringComparison.OrdinalIgnoreCase));
            if (tagToRemove == null) return;
            macroTags.Entries.Remove(tagToRemove);
            configuration.SetTagData(MacroTag, macroTags);
        }

        public async Task<IEnumerable<MacroEntry>> GetServerMacros(IGuild guild) =>
            await GetServerMacros(guild.Id);

        public async Task<IEnumerable<MacroEntry>> GetServerMacros(ulong guildId)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(guildId);
            return configuration.GetOrAddTagData(MacroTag, () => new MacroServerEntries()).Entries;
        }

    }
}
