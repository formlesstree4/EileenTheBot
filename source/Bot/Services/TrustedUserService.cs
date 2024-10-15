using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Database.Repositories;

namespace Bot.Services;

/// <summary>
/// A service that manages trusted users.
/// </summary>
public sealed class TrustedUserService : IEileenService
{
    private readonly DiscordRepository _discordRepository;
    private readonly UserService _userService;
    private readonly List<ulong> _trustedUsers = new();
    private readonly Dictionary<ulong, List<ulong>> _trustedGuildUsers = new();

    public TrustedUserService(DiscordRepository discordRepository, UserService userService)
    {
        _discordRepository = discordRepository;
        _userService = userService;
    }

    /// <summary>
    /// Initializes the service.
    /// </summary>
    public async Task InitializeService()
    {
        var adminUsers = await _discordRepository.GetAdminUsersAsync();
        foreach (var user in adminUsers)
        {
            _trustedUsers.Add(user.user_id);
        }
        var guilds = await _discordRepository.GetGuildConfigurationsAsync();
        foreach (var guild in guilds)
        {
            _trustedGuildUsers.Add(guild.guild_id, guild.trusted_users?.ToList() ?? new());
        }
    }

    /// <summary>
    /// Gets a value indicating whether the specified user is a trusted user.
    /// </summary>
    /// <param name="userId">The discord user ID snowflake</param>
    /// <returns>A value indicating whether the specified user is a trusted user. Please note, this effectively means that the user is an admin.</returns>
    public bool IsTrustedUser(ulong userId)
    {
        return _trustedUsers.Contains(userId);
    }

    /// <summary>
    /// Gets a value indicating whether the specified user is a trusted user in the specified guild.
    /// </summary>
    /// <param name="guildId">The discord guild ID snowflake</param>
    /// <param name="userId">The discord user ID snowflake</param>
    /// <returns>A value indicating whether the specified user is a trusted user in the specified guild.</returns>
    public bool IsTrustedGuildUser(ulong guildId, ulong userId)
    {
        return _trustedGuildUsers.TryGetValue(guildId, out var users) && users.Contains(userId);
    }

    /// <summary>
    /// Adds a trusted user.
    /// </summary>
    /// <param name="userId">The discord user ID snowflake</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddTrustedUserAsync(ulong userId)
    {
        if (_trustedUsers.Contains(userId)) return;
        var userData = await _userService.GetOrCreateUserData(userId);
        await _discordRepository.UpdateUserConfigurationAsync(userId, userData.Money, userData.Loaned, true);
        _trustedUsers.Add(userId);
    }

    /// <summary>
    /// Removes a trusted user.
    /// </summary>
    /// <param name="userId">The discord user ID snowflake</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RemoveTrustedUserAsync(ulong userId)
    {
        if (!_trustedUsers.Contains(userId)) return;
        _trustedUsers.Remove(userId);
        var userData = await _userService.GetOrCreateUserData(userId);
        await _discordRepository.UpdateUserConfigurationAsync(userId, userData.Money, userData.Loaned, false);
    }
}
