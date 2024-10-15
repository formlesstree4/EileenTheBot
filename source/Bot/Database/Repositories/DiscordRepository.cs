using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models.Database.Discord;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bot.Database.Repositories;

public sealed class DiscordRepository : PgSqlRepository<DiscordRepository>
{
    public DiscordRepository(
        IConfiguration configuration,
        ILogger<DiscordRepository> logger) : base(configuration.GenerateConnectionString(), configuration, logger)
    {
    }

    // guild configuration
    public async Task<GuildConfigurationModel?> GetGuildConfigurationAsync(ulong guildId)
    {
        var procedureName = GetProcedureName("get_guild_configuration", "discord.get_guild_configuration");
        var query = $"SELECT * FROM {procedureName}(?)";
        var parameters = new DynamicParameters();
        Add("i_guild_id", guildId, parameters);
        var results = await ExecuteQueryAsync<GuildConfigurationModel>(query, parameters);
        return results.FirstOrDefault();
    }
    public async Task<IList<GuildConfigurationModel>> GetGuildConfigurationsAsync()
    {
        var procedureName = GetProcedureName("get_guild_configurations", "discord.get_guild_configurations");
        var query = $"SELECT * FROM {procedureName}()";
        var results = await ExecuteQueryAsync<GuildConfigurationModel>(query);
        return results.AsList();
    }
    public async Task CreateGuildConfigurationAsync(ulong guildId)
    {
        var procedureName = GetProcedureName("create_guild_configuration", "discord.create_guild_configuration");
        var query = $"SELECT {procedureName}(?)";
        var parameters = new DynamicParameters();
        Add("i_guild_id", guildId, parameters);
        await ExecuteAsync(query, parameters);
    }
    public async Task UpdateGuildConfigurationAsync(ulong guildId, List<ulong> trustedUsers, bool enabled)
    {
        var procedureName = GetProcedureName("update_guild_configuration", "discord.update_guild_configuration");
        var query = $"SELECT {procedureName}(?, ?, ?)";
        var parameters = new DynamicParameters();
        Add("i_guild_id", guildId, parameters);
        Add("i_trusted_users", trustedUsers.ToArray(), parameters);
        Add("i_enabled", enabled, parameters);
        await ExecuteAsync(query, parameters);
    }

    // user configuration
    public async Task<UserConfigurationModel?> GetUserConfigurationAsync(ulong userId)
    {
        var procedureName = GetProcedureName("get_user", "discord.get_user");
        var query = $"SELECT * FROM {procedureName}(?)";
        var parameters = new DynamicParameters();
        Add("i_user_id", userId, parameters);
        var results = await ExecuteQueryAsync<UserConfigurationModel>(query, parameters);
        return results.FirstOrDefault();
    }
    public async Task CreateUserConfigurationAsync(ulong userId)
    {
        var procedureName = GetProcedureName("create_user", "discord.create_user");
        var query = $"SELECT {procedureName}(?)";
        var parameters = new DynamicParameters();
        Add("i_user_id", userId, parameters);
        await ExecuteAsync(query, parameters);
    }
    public async Task<IList<UserConfigurationModel>> GetAdminUsersAsync()
    {
        var procedureName = GetProcedureName("get_admin_users", "discord.get_admin_users");
        var query = $"SELECT * FROM {procedureName}()";
        var results = await ExecuteQueryAsync<UserConfigurationModel>(query);
        return results.AsList();
    }
    public async Task UpdateUserConfigurationAsync(ulong userId, ulong money, ulong loaned, bool admin)
    {
        var procedureName = GetProcedureName("update_user", "discord.update_user");
        var query = $"SELECT {procedureName}(?, ?, ?, ?)";
        var parameters = new DynamicParameters();
        Add("i_user_id", userId, parameters);
        Add("i_money", money, parameters);
        Add("i_loaned", loaned, parameters);
        Add("i_admin", admin, parameters);
        await ExecuteAsync(query, parameters);
    }
    public async Task UpdateUserFinancesAsync(ulong userId, ulong money, ulong loaned)
    {
        var procedureName = GetProcedureName("update_user_finances", "discord.update_user_finances");
        var query = $"SELECT {procedureName}(?, ?, ?)";
        var parameters = new DynamicParameters();
        Add("i_user_id", userId, parameters);
        Add("i_money", money, parameters);
        Add("i_loaned", loaned, parameters);
        await ExecuteAsync(query, parameters);
    }
    public async Task InsertActionLogAsync(ulong userId, ulong? guildId, ulong? channelId, ulong? threadId,
        string action)
    {
        var procedureName = GetProcedureName("insert_action_log", "discord.insert_action_log");
        var query = $"SELECT {procedureName}(?, ?, ?, ?, ?)";
        var parameters = new DynamicParameters();
        Add("i_user_id", userId, parameters);
        Add("i_guild_id", guildId, parameters);
        Add("i_channel_id", channelId, parameters);
        Add("i_thread_id", threadId, parameters);
        Add("i_action", action, parameters);
        await ExecuteAsync(query, parameters);
    }

}
