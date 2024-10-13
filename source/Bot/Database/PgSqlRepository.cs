using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bot.Database;

/// <summary>
/// The base repository class for any database interactions
/// </summary>
/// <typeparam name="TRepository"></typeparam>
public abstract class PgSqlRepository<TRepository>
    where TRepository : PgSqlRepository<TRepository>
{

    private const string RepoSection = "Repositories";
    private const string Timeout = "timeout";
    private const int DefaultTimeoutInSeconds = 30;
    private readonly string _defaultConnectionString;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TRepository> _logger;
    private readonly string _name = typeof(TRepository).Name;

    private static readonly Dictionary<Type, DbType> SqlMappings = new();

    /// <summary>
    /// Gets the logger for this repository
    /// </summary>
    protected ILogger<TRepository> Logger => _logger;

    /// <summary>
    /// Static initializer for the repository class
    /// </summary>
    static PgSqlRepository()
    {
        // Just reusing this for PgSql
        SqlMappings.Add(typeof(long), DbType.Int64);
        SqlMappings.Add(typeof(long?), DbType.Int64);
        SqlMappings.Add(typeof(byte[]), DbType.Binary);
        SqlMappings.Add(typeof(bool), DbType.Boolean);
        SqlMappings.Add(typeof(bool?), DbType.Boolean);
        SqlMappings.Add(typeof(string), DbType.String);
        SqlMappings.Add(typeof(char[]), DbType.String);
        SqlMappings.Add(typeof(DateTime), DbType.DateTime);
        SqlMappings.Add(typeof(DateTime?), DbType.DateTime);
        SqlMappings.Add(typeof(DateTimeOffset), DbType.DateTimeOffset);
        SqlMappings.Add(typeof(DateTimeOffset?), DbType.DateTimeOffset);
        SqlMappings.Add(typeof(decimal), DbType.Decimal);
        SqlMappings.Add(typeof(decimal?), DbType.Decimal);
        SqlMappings.Add(typeof(double), DbType.Double);
        SqlMappings.Add(typeof(double?), DbType.Double);
        SqlMappings.Add(typeof(int), DbType.Int32);
        SqlMappings.Add(typeof(int?), DbType.Int32);
        SqlMappings.Add(typeof(short), DbType.Int16);
        SqlMappings.Add(typeof(short?), DbType.Int16);
        SqlMappings.Add(typeof(Guid), DbType.Guid);
        SqlMappings.Add(typeof(Guid?), DbType.Guid);

        // PgSql NATIVELY supports arrays! No custom types necessary!
        SqlMappings.Add(typeof(int[]), DbType.Object);
        SqlMappings.Add(typeof(long[]), DbType.Object);
        SqlMappings.Add(typeof(DateTime[]), DbType.Object);
        SqlMappings.Add(typeof(DateTimeOffset[]), DbType.Object);
        SqlMappings.Add(typeof(decimal[]), DbType.Object);
        SqlMappings.Add(typeof(double[]), DbType.Object);
        SqlMappings.Add(typeof(float[]), DbType.Object);
        SqlMappings.Add(typeof(short[]), DbType.Object);
    }

    /// <summary>
    /// Creates a new <see cref="PgSqlRepository{TRepository}"/>
    /// </summary>
    /// <param name="defaultConnectionString">The default connection string to use</param>
    /// <param name="configuration"><see cref="IConfiguration"/></param>
    /// <param name="logger"><see cref="ILogger{TCategoryName}"/></param>
    public PgSqlRepository(
        string? defaultConnectionString,
        IConfiguration configuration,
        ILogger<TRepository> logger)
    {
        if (string.IsNullOrWhiteSpace(defaultConnectionString))
        {
            throw new ArgumentException("Must be a valid connection string", nameof(defaultConnectionString));
        }
        _defaultConnectionString = defaultConnectionString;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets an optional <see cref="IConfigurationSection"/> for this particular <typeparamref name="TRepository"/>
    /// </summary>
    /// <returns>In the context of a configuration JSON file, there would be a root section called 'Repositories' and a child section called '<typeparamref name="TRepository"/>'. This method is a shortcut to return that section.</returns>
    private IConfigurationSection GetRepositoryConfiguration() => _configuration.GetSection(RepoSection).GetSection(_name);

    /// <summary>
    /// Returns the string name for a stored procedure
    /// </summary>
    /// <param name="key">The lookup key for the procedure in the configuration file</param>
    /// <param name="default">The default value to return if there is no available entry in the configuration file</param>
    /// <returns>String</returns>
    protected string GetProcedureName(string key, string @default) => GetRepositoryConfiguration().GetValue(key, @default) ?? @default;

    /// <summary>
    /// Adds a given value to the supplied parameters
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name">The name of the parameter</param>
    /// <param name="value">The value of the parameter. Ignored if <paramref name="isOutput"/> is true</param>
    /// <param name="parameters">The <see cref="DynamicParameters"/> to manipulate</param>
    /// <param name="isOutput">If true, this is an output parameter</param>
    protected void Add<T>(string name, T value, DynamicParameters parameters, bool isOutput = false)
    {
        if (!SqlMappings.TryGetValue(typeof(T), out var dbType))
        {
            throw new ArgumentException("Invalid Type Specified", nameof(T));
        }
        parameters.Add(name, value, dbType, isOutput ? ParameterDirection.Output : ParameterDirection.Input);
    }

    /// <summary>
    /// Gets the appropriate timeout for command execution in seconds.
    /// </summary>
    /// <remarks>
    /// This is set at the repository level and is not customizable per command. Defaults to <see cref="DefaultTimeoutInSeconds"/>
    /// </remarks>
    private int GetCommandTimeout => GetRepositoryConfiguration().GetValue(Timeout, DefaultTimeoutInSeconds);

    /// <summary>
    /// Asynchronously executes a stored procedure with the given parameters
    /// </summary>
    /// <param name="procedureName">The name of the procedure</param>
    /// <param name="parameters"><see cref="DynamicParameters"/></param>
    /// <param name="connectionString">An optional connection string to override with</param>
    /// <returns>A promise to execute a stored procedure</returns>
    public async Task ExecuteAsync(string procedureName, DynamicParameters? parameters = null, string? connectionString = null)
    {
        try
        {
            await using var connection = await GetConnectionAsync(connectionString);
            await connection.ExecuteAsync(procedureName, parameters ?? new(), commandType: CommandType.StoredProcedure, commandTimeout: GetCommandTimeout);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occurred executing the procedure {procedureName}", procedureName);
        }
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with the given parameters
    /// </summary>
    /// <param name="procedureName">The name of the procedure</param>
    /// <param name="parameters"><see cref="DynamicParameters"/></param>
    /// <param name="connectionString">An optional connection string to override with</param>
    /// <returns>A promise to execute a stored procedure and return the first column on the first row</returns>
    public async Task<T?> ExecuteScalarAsync<T>(string procedureName, DynamicParameters? parameters = null, string? connectionString = null)
    {
        try
        {
            await using var connection = await GetConnectionAsync(connectionString);
            return await connection.ExecuteScalarAsync<T>(procedureName, parameters ?? new(), commandType: CommandType.StoredProcedure, commandTimeout: GetCommandTimeout);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occurred executing the procedure {procedureName}", procedureName);
            return default;
        }
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with the given parameters
    /// </summary>
    /// <param name="procedureName">The name of the procedure</param>
    /// <param name="parameters"><see cref="DynamicParameters"/></param>
    /// <param name="connectionString">An optional connection string to override with</param>
    /// <returns>A promise to execute a stored procedure and return the entire result set cast to a collection of type <typeparamref name="T"/></returns>
    public async Task<IEnumerable<T>> ExecuteQueryAsync<T>(string procedureName, DynamicParameters? parameters = null, string? connectionString = null)
    {
        try
        {
            await using var connection = await GetConnectionAsync(connectionString);
            return await connection.QueryAsync<T>(procedureName, parameters ?? new(), commandType: CommandType.StoredProcedure, commandTimeout: GetCommandTimeout);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occurred executing the procedure {procedureName}", procedureName);
            return Enumerable.Empty<T>();
        }
    }

    /// <summary>
    /// Asynchronously executes raw SQL with the given parameters
    /// </summary>
    /// <param name="sql">The raw SQL to execute</param>
    /// <param name="parameters"><see cref="DynamicParameters"/></param>
    /// <param name="connectionString">An optional connection string to override with</param>
    /// <returns>A promise to execute raw SQL</returns>
    public async Task ExecuteRawAsync(string sql, DynamicParameters? parameters = null, string? connectionString = null)
    {
        try
        {
            await using var connection = await GetConnectionAsync(connectionString);
            await connection.ExecuteAsync(sql, parameters ?? new(), commandType: CommandType.Text, commandTimeout: GetCommandTimeout);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occurred executing the following SQL:\r\n{sql}", sql);
        }
    }

    /// <summary>
    /// Asynchronously executes raw SQL with the given parameters
    /// </summary>
    /// <param name="sql">The raw SQL to execute</param>
    /// <param name="parameters"><see cref="DynamicParameters"/></param>
    /// <param name="connectionString">An optional connection string to override with</param>
    /// <returns>A promise to execute raw SQL and return the first column on the first row</returns>
    public async Task<T?> ExecuteScalarRawAsync<T>(string sql, DynamicParameters? parameters = null, string? connectionString = null)
    {
        try
        {
            await using var connection = await GetConnectionAsync(connectionString);
            return await connection.ExecuteScalarAsync<T>(sql, parameters ?? new(), commandType: CommandType.Text, commandTimeout: GetCommandTimeout);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occurred executing the following SQL:\r\n{sql}", sql);
            return default;
        }
    }

    /// <summary>
    /// Asynchronously executes raw SQL with support for parameters
    /// </summary>
    /// <typeparam name="T">The expected return type</typeparam>
    /// <param name="sql">The raw SQL to execute</param>
    /// <param name="parameters"><see cref="DynamicParameters"/></param>
    /// <param name="connectionString">An optional connection string to override with</param>
    /// <returns>A promise to execute the supplied SQL and return the entire result set cast to a collection of type <typeparamref name="T"/></returns>
    public async Task<IEnumerable<T>> ExecuteRawQueryAsync<T>(string sql, DynamicParameters? parameters = null, string? connectionString = null)
    {
        try
        {
            await using var connection = await GetConnectionAsync(connectionString);
            return await connection.QueryAsync<T>(sql, parameters ?? new(), commandType: CommandType.Text, commandTimeout: GetCommandTimeout);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occurred executing the following SQL:\r\n{sql}", sql);
            return Enumerable.Empty<T>();
        }
    }

    /// <summary>
    /// Creates a new <see cref="NpgsqlConnection"/>, opens it, and returns it
    /// </summary>
    /// <param name="connectionString">Optionally overrides the connection string provided</param>
    /// <returns><see cref="NpgsqlConnection"/></returns>
    private async Task<NpgsqlConnection> GetConnectionAsync(string? connectionString = null)
    {
        var connection = new NpgsqlConnection(connectionString ?? _defaultConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    /// Generates the formatted parameter list
    /// </summary>
    /// <param name="parameters"><see cref="DynamicParameters"/></param>
    /// <returns>A well-formatted string</returns>
    /// <remarks>The order in which parameters are added is VERY important as that is the order in which these get spit out</remarks>
    private static string GenerateParameterList(DynamicParameters parameters)
    {
        var formattedNames = parameters.ParameterNames.Select(pn => $"@{pn}");
        var parameterList = string.Join(",", formattedNames);
        return parameterList;
    }

}
