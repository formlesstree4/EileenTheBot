using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Bot.Database.Repositories;
using Bot.Models.Eileen;
using Discord;

namespace Bot.Services;

/// <summary>
/// The UserService is responsible for caching the User data and managing the user's finances.
/// </summary>
public sealed class UserService : IEileenService
{
    private readonly DiscordRepository _discordRepository;
    private readonly Dictionary<ulong, EileenUserData> _userContent = new();
    public UserService(DiscordRepository discordRepository)
    {
        _discordRepository = discordRepository;
    }

    /// <summary>
    /// Gets the user data for the specified user.
    /// </summary>
    /// <param name="user">The discord user</param>
    /// <returns>The user data for the specified user.</returns>
    public async Task<EileenUserData> GetOrCreateUserData(IUser user)
        => await GetOrCreateUserData(user.Id);

    /// <summary>
    /// Gets the user data for the specified user ID.
    /// </summary>
    /// <param name="userId">The discord user ID snowflake</param>
    /// <returns>The user data for the specified user ID.</returns>
    public async Task<EileenUserData> GetOrCreateUserData(ulong userId)
    {
        if (_userContent.TryGetValue(userId, out var userData)) return userData;
        var userDataModel = await _discordRepository.GetUserConfigurationAsync(userId);
        if (userDataModel is null)
        {
            await _discordRepository.CreateUserConfigurationAsync(userId);
            userDataModel = await _discordRepository.GetUserConfigurationAsync(userId);
        };
        Debug.Assert(userDataModel != null, nameof(userDataModel) + " != null");
        userData = new EileenUserData
        {
            UserId = userId,
            Money = userDataModel.money,
            Loaned = userDataModel.loaned,
        };
        _userContent.Add(userId, userData);
        return userData;
    }

    /// <summary>
    /// Commits the user data to the database.
    /// </summary>
    /// <param name="userData">The user data to commit.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task UpdateUserData(EileenUserData userData)
    {
        await _discordRepository.UpdateUserFinancesAsync(userData.UserId, userData.Money, userData.Loaned);
    }

    /// <summary>
    /// Flushes all user data to the database.
    /// </summary>
    public async Task SaveServiceAsync()
    {
        foreach (var user in _userContent.Values)
        {
            await UpdateUserData(user);
        }
    }

}
