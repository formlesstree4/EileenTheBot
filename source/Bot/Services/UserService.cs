using Bot.Models.Eileen;
using Bot.Services.Communication;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hangfire;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Services
{

    [Summary("Maintains the relationship between EileenUserData and Discord IUser. Any and all services that interact with a Profile (such as Dungeoneering) go through this service in order provide data persistency automatically")]
    public sealed class UserService : IEileenService
    {

        private readonly ILogger<UserService> _logger;
        private readonly RavenDatabaseService _ravenDatabaseService;
        private readonly BetterPaginationService _paginationService;
        private readonly DiscordSocketClient _client;
        private readonly StupidTextService _stupidTextService;
        private readonly HangfireToDiscordComm _hangfireToDiscordComm;
        private readonly ConcurrentDictionary<ulong, EileenUserData> _userContent;
        private readonly List<Func<ProfileCallback, Task<ProfileCallback>>> _profilePageCallbacks;

        public UserService(
            RavenDatabaseService ravenDatabaseService,
            BetterPaginationService paginationService,
            DiscordSocketClient client,
            StupidTextService stupidTextService,
            HangfireToDiscordComm hangfireToDiscordComm,
            ILogger<UserService> logger)
        {
            _userContent = new ConcurrentDictionary<ulong, EileenUserData>();
            _profilePageCallbacks = new List<Func<ProfileCallback, Task<ProfileCallback>>>();
            _ravenDatabaseService = ravenDatabaseService;
            _paginationService = paginationService;
            _client = client;
            _stupidTextService = stupidTextService;
            _hangfireToDiscordComm = hangfireToDiscordComm;
            _logger = logger;
        }

        public async Task InitializeService()
        {
            _logger.LogInformation("Initializing the UserService");
            await LoadServiceAsync();
            _logger.LogInformation("Setting up recurring jobs...");
            RecurringJob.AddOrUpdate("usersAutoSave", () => SaveServiceAsync(), Cron.Hourly());
            RecurringJob.AddOrUpdate("usersUpdateServerPresence", () => UpdateUserDataServerAwareness(), Cron.Hourly());
            RecurringJob.AddOrUpdate("tellCoolswiftHello", () => _hangfireToDiscordComm.SendMessageToUser(143551309776289792, "Hey mom!"), Cron.Hourly);
            _logger.LogInformation("UserService has been initialized");
            await Task.Yield();
        }

        public async Task SaveServiceAsync()
        {
            using var session = _ravenDatabaseService.GetOrAddDocumentStore("erector_users").OpenAsyncSession();
            _logger.LogInformation("Saving User Data to RavenDB...");
            foreach (var entry in _userContent)
            {
                _logger.LogTrace("Saving {entry}...", entry.Key);
                await session.StoreAsync(entry.Value, entry.Key.ToString());
            }
            await session.SaveChangesAsync();
            _logger.LogInformation("User Data has been saved");
        }

        public async Task LoadServiceAsync()
        {
            using var session = _ravenDatabaseService.GetOrAddDocumentStore("erector_users").OpenAsyncSession();
            _logger.LogInformation("Loading User Data from RavenDB...");
            var c = await session.Query<EileenUserData>().ToListAsync();
            _logger.LogInformation("Discovered {count} item(s) to load!", c.Count);
            foreach (var userData in c)
            {
                _userContent.TryAdd(userData.UserId, userData);
            }
        }

        public bool AutoInitialize() => false;

        public async Task<EileenUserData> GetOrCreateUserData(IUser user)
            => await GetOrCreateUserData(user.Id);

        public async Task<EileenUserData> GetOrCreateUserData(ulong userId)
        {
            _logger.LogInformation("Retrieving UserData for {userId}", userId);
            if (!_userContent.ContainsKey(userId))
            {
                _userContent.TryAdd(userId, await (CreateUserContent(userId)));
            }
            return GetUserData(userId);
        }

        public async Task CreateUserProfileMessage(IUser user, IInteractionContext context)
            => await CreateUserProfileMessage(user.Id, context);

        public async Task CreateUserProfileMessage(ulong userId, IInteractionContext context)
        {
            _logger.LogInformation($"Generating the User Profile message...");
            var userData = await GetOrCreateUserData(userId);
            var discordInfo = await (_client as IDiscordClient).GetUserAsync(userId);
            var mainProfilePageBuilder =
                new EmbedBuilder()
                    .WithTitle("About Me")
                    .AddField(new EmbedFieldBuilder()
                        .WithName("Description")
                        .WithValue(!string.IsNullOrWhiteSpace(userData.Description) ? userData.Description : "_No Description Set_")
                        .WithIsInline(false))
                    .AddField(new EmbedFieldBuilder()
                        .WithName("Created")
                        .WithValue(userData.Created.ToString("yyyy-MM-dd"))
                        .WithIsInline(true))
                    .AddField(new EmbedFieldBuilder()
                        .WithName("Servers In")
                        .WithValue(userData.ServersOn.Count.ToString("N0"))
                        .WithIsInline(true));
            EnsureDefaults(mainProfilePageBuilder, discordInfo, userData);
            var mainProfilePage = mainProfilePageBuilder.Build();
            var profilePages = new List<Embed> { mainProfilePage };
            foreach (var callback in _profilePageCallbacks)
            {
                var pcb = new ProfileCallback(userData, discordInfo, new EmbedBuilder());
                var pcbResult = await callback(pcb);
                EnsureDefaults(pcbResult.PageBuilder, discordInfo, userData);
                profilePages.Add(pcbResult.PageBuilder.Build());
            }
            await _paginationService.Send(context, context.Channel, new BetterPaginationMessage(profilePages, profilePages.Count > 1, context.User));
        }

        public async Task UpdateUserDataServerAwareness()
        {
            _logger.LogInformation("Synchronizing Users Server List with available Guilds");
            var servers = _client.Guilds.ToList();
            foreach (var ud in _userContent)
            {
                _logger.LogTrace("Identifying Servers {userId} is located on", ud.Key);
                ud.Value.ServersOn = (from c in servers
                                      where c.GetUser(ud.Key) is not null
                                      select c.Id).ToList();
            }
            _logger.LogTrace($"Synchronization Complete");
            BackgroundJob.Schedule(() => _hangfireToDiscordComm.SendMessageToUser(105497358833336320, "Awareness Updated!"), TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        public void RegisterProfileCallback(Func<ProfileCallback, Task<ProfileCallback>> callback)
        {
            _logger.LogTrace("A profile callback is being registered with the UserService");
            _profilePageCallbacks.Add(callback);
        }

        public IEnumerable<EileenUserData> WalkUsers() => _userContent.Values.ToList();

        private void EnsureDefaults(EmbedBuilder builder, IUser discordInfo, EileenUserData userData)
        {
            builder.WithAuthor(new EmbedAuthorBuilder()
                    .WithName(builder?.Author?.Name ?? $"Profile For {discordInfo.Username}")
                    .WithIconUrl(discordInfo.GetAvatarUrl() ?? discordInfo.GetDefaultAvatarUrl()))
                    .WithColor(new Color(152, 201, 124))
                    .WithCurrentTimestamp()
                    .WithFooter(_stupidTextService.GetRandomStupidText());
            if (!string.IsNullOrWhiteSpace(userData.ProfileImage) && string.IsNullOrWhiteSpace(builder.ThumbnailUrl))
            {
                builder.WithThumbnailUrl(userData.ProfileImage);
            }
        }

        private EileenUserData GetUserData(ulong userId)
        {
            _logger.LogInformation("Retrieving UserData for {userId} from cache (aka NOT going to RavenDB)", userId);
            if (!_userContent.TryGetValue(userId, out var d))
            {
                throw new ArgumentException(null, nameof(userId));
            }
            return d;
        }

        private async Task<EileenUserData> CreateUserContent(ulong userId)
        {
            _logger.LogInformation("Creating new UserData for {userId}", userId);
            var userData = new EileenUserData { UserId = userId };
            using (var session = _ravenDatabaseService.GetOrAddDocumentStore("erector_users").OpenAsyncSession())
            {
                await session.StoreAsync(userData, userId.ToString());
                await session.SaveChangesAsync();
            }
            return userData;
        }

    }

}
