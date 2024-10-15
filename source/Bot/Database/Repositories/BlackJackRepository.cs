using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.Models.Database.BlackJack;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bot.Database.Repositories;

public sealed class BlackJackRepository : PgSqlRepository<BlackJackRepository>
{
    public BlackJackRepository(
        IConfiguration configuration,
        ILogger<BlackJackRepository> logger) : base(configuration.GenerateConnectionString(), configuration, logger)
    {}

    // table stuff first
    public async Task CreateTableAsync(Guid tableId, ulong guildId, ulong channelId, ulong threadId)
    {
        var procedureName = GetProcedureName("create_table", "blackjack.create_table");
        var parameters = new DynamicParameters();
        Add("i_table_id", tableId, parameters);
        Add("i_guild_id", guildId, parameters);
        Add("i_channel_id", channelId, parameters);
        Add("i_thread_id", threadId, parameters);
        await ExecuteAsync(procedureName, parameters);
    }

    public async Task<IList<TableModel>> GetTablesAsync()
    {
        var procedureName = GetProcedureName("get_tables", "blackjack.get_tables");
        var parameters = new DynamicParameters();
        var results = await ExecuteQueryAsync<TableModel>(procedureName, parameters);
        return results.AsList();
    }

    public async Task UpdateTableAsync(Guid tableId, bool active, ulong roundsPlayed)
    {
        var procedureName = GetProcedureName("update_table", "blackjack.update_table");
        var parameters = new DynamicParameters();
        Add("i_table_id", tableId, parameters);
        Add("i_active", active, parameters);
        Add("i_rounds_played", roundsPlayed, parameters);
        await ExecuteAsync(procedureName, parameters);
    }

    public async Task CreateGuildConfigurationAsync(ulong guildId, ulong[] managers)
    {
        var procedureName = GetProcedureName("create_guild_configuration", "blackjack.create_guild_configuration");
        var parameters = new DynamicParameters();
        Add("i_guild_id", guildId, parameters);
        Add("i_managers", managers, parameters);
        await ExecuteAsync(procedureName, parameters);
    }

    public async Task<IList<GuildConfigurationModel>> GetGuildConfigurations()
    {
        var procedureName = GetProcedureName("get_guild_configurations", "blackjack.get_guild_configurations");
        var parameters = new DynamicParameters();
        var results = await ExecuteQueryAsync<GuildConfigurationModel>(procedureName, parameters);
        return results.AsList();
    }

    public async Task UpdateGuildConfigurationAsync(ulong guildId, ulong[] managers, ulong[] allowedChannels,
        decimal maxLoanAmount)
    {
        var procedureName = GetProcedureName("update_guild_configuration", "blackjack.update_guild_configuration");
        var parameters = new DynamicParameters();
        Add("i_guild_id", guildId, parameters);
        Add("i_managers", managers, parameters);
        Add("i_allowed_channels", allowedChannels, parameters);
        Add("i_max_loan_amount", maxLoanAmount, parameters);
        await ExecuteAsync(procedureName, parameters);
    }

    public async Task CreatePlayerStatisticsAsync(ulong userId)
    {
        var procedureName = GetProcedureName("create_player_statistics", "blackjack.create_player_statistics");
        var parameters = new DynamicParameters();
        Add("i_user_id", userId, parameters);
        await ExecuteAsync(procedureName, parameters);
    }

    public async Task<IList<PlayerStatisticsModel>> GetPlayerStatisticsAsync()
    {
        var procedureName = GetProcedureName("get_player_statistics", "blackjack.get_player_statistics");
        var parameters = new DynamicParameters();
        var results = await ExecuteQueryAsync<PlayerStatisticsModel>(procedureName, parameters);
        return results.AsList();
    }

    public async Task UpdatePlayerStatisticsAsync(ulong userId, decimal totalMoneyWon, decimal totalMoneyLost,
        decimal totalMoneyBet, ulong totalRoundsWon, ulong totalRoundsLost, ulong totalRoundsPlayed)
    {
        var procedureName = GetProcedureName("update_player_statistics", "blackjack.update_player_statistics");
        var parameters = new DynamicParameters();
        Add("i_user_id", userId, parameters);
        Add("i_total_money_won", totalMoneyWon, parameters);
        Add("i_total_money_lost", totalMoneyLost, parameters);
        Add("i_total_money_bet", totalMoneyBet, parameters);
        Add("i_total_rounds_won", totalRoundsWon, parameters);
        Add("i_total_rounds_lost", totalRoundsLost, parameters);
        Add("i_total_rounds_played", totalRoundsPlayed, parameters);
        await ExecuteAsync(procedureName, parameters);
    }

}
