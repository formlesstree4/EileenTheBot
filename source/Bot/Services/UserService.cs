using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models;
using Bot.Services.Communication;
using Bot.Services.RavenDB;
using Discord;
using Discord.WebSocket;
using Hangfire;
using Raven.Client.Documents;

namespace Bot.Services
{

    public sealed class UserService
    {

        private readonly Func<LogMessage, Task> logger;
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly BetterPaginationService paginationService;
        private readonly DiscordSocketClient client;
        private readonly StupidTextService stupidTextService;
        private readonly HangfireToDiscordComm hangfireToDiscordComm;
        private readonly ConcurrentDictionary<ulong, EileenUserData> userContent;
        private readonly List<Func<ProfileCallback, Task<ProfileCallback>>> profilePageCallbacks;

        public UserService(
            RavenDatabaseService ravenDatabaseService,
            BetterPaginationService paginationService,
            DiscordSocketClient client,
            StupidTextService stupidTextService,
            HangfireToDiscordComm hangfireToDiscordComm,
            Func<LogMessage, Task> logger)
        {
            this.userContent = new ConcurrentDictionary<ulong, EileenUserData>();
            this.profilePageCallbacks = new List<Func<ProfileCallback, Task<ProfileCallback>>>();
            this.ravenDatabaseService = ravenDatabaseService;
            this.paginationService = paginationService;
            this.client = client;
            this.stupidTextService = stupidTextService;
            this.hangfireToDiscordComm = hangfireToDiscordComm;
            this.logger = logger;
        }

        public async Task InitializeService()
        {
            Write("Initializing the UserService");
            await LoadServiceAsync();
            Write("Setting up recurring jobs...");
            RecurringJob.AddOrUpdate("usersAutoSave", () => SaveServiceAsync(), Cron.Minutely());
            RecurringJob.AddOrUpdate("usersUpdateServerPresence", () => UpdateUserDataServerAwareness(), Cron.Hourly());
            RecurringJob.AddOrUpdate("tellCoolswiftHello", () => hangfireToDiscordComm.SendMessageToUser(143551309776289792, "Hey mom!"), Cron.Hourly);
            Write("UserService has been initialized");
            await Task.Yield();
        }

        public async Task SaveServiceAsync()
        {
            using(var session = ravenDatabaseService.GetOrAddDocumentStore("erector_users").OpenAsyncSession())
            {
                Write($"Saving User Data to RavenDB...");
                foreach (var entry in userContent)
                {
                    Write($"Saving {entry.Key}...", LogSeverity.Verbose);
                    await session.StoreAsync(entry.Value, entry.Key.ToString());
                }
                await session.SaveChangesAsync();
                Write("User Data has been saved");
            }
        }

        public async Task LoadServiceAsync()
        {
            using(var session = ravenDatabaseService.GetOrAddDocumentStore("erector_users").OpenAsyncSession())
            {
                Write($"Loading User Data from RavenDB...");
                var c = await session.Query<EileenUserData>().ToListAsync();
                Write($"Discovered {c.Count} item(s) to load!");
                foreach(var userData in c)
                {
                    userContent.TryAdd(userData.UserId, userData);
                }
            }
        }

        public async Task<EileenUserData> GetOrCreateUserData(ulong userId)
        {
            Write($"Retrieving UserData for {userId}");
            if (!userContent.ContainsKey(userId))
            {
                userContent.TryAdd(userId, await(CreateUserContent(userId)));
            }
            return GetUserData(userId);
        }

        public async Task CreateUserProfileMessage(ulong userId, IMessageChannel channel)
        {
            Write($"Generating the User Profile message...");
            var userData = await GetOrCreateUserData(userId);
            var discordInfo = await (client as IDiscordClient).GetUserAsync(userId);
            var mainProfilePageBuilder =
                new EmbedBuilder()
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
            foreach(var callback in profilePageCallbacks)
            {
                var pcb = new ProfileCallback(userData, discordInfo, new EmbedBuilder());
                var pcbResult = await callback(pcb);
                EnsureDefaults(pcbResult.PageBuilder, discordInfo, userData);
                profilePages.Add(pcbResult.PageBuilder.Build());
            }
            await paginationService.Send(channel, new BetterPaginationMessage(profilePages, profilePages.Count > 1, discordInfo));
        }

        public async Task UpdateUserDataServerAwareness()
        {
            Write("Synchronizing Users Server List with available Guilds");
            var servers = client.Guilds.ToList();
            foreach(var ud in userContent)
            {
                Write($"Identifying Servers {ud.Key} is located on", LogSeverity.Verbose);
                ud.Value.ServersOn = (from c in servers
                                      where !ReferenceEquals(c.GetUser(ud.Key), null)
                                      select c.Id).ToList();
                Write($"{ud.Key} is on {ud.Value.ServersOn.Count} server(s) out of {servers.Count}", LogSeverity.Verbose);
            }
            Write($"Synchronization Complete");
            BackgroundJob.Schedule(() => hangfireToDiscordComm.SendMessageToUser(105497358833336320, "Awareness Updated!"), TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        public void RegisterProfileCallback(Func<ProfileCallback, Task<ProfileCallback>> callback)
        {
            Write("A profile callback is being registered with the UserService", LogSeverity.Verbose);
            profilePageCallbacks.Add(callback);
        }

        public IEnumerable<EileenUserData> WalkUsers() => userContent.Values.ToList();

        private void EnsureDefaults(EmbedBuilder builder, IUser discordInfo, EileenUserData userData)
        {
            builder.WithAuthor(new EmbedAuthorBuilder()
                    .WithName(builder?.Author?.Name ?? $"Profile For {discordInfo.Username}")
                    .WithIconUrl(discordInfo.GetAvatarUrl() ?? discordInfo.GetDefaultAvatarUrl()))
                    .WithColor(new Color(152, 201, 124))
                    .WithCurrentTimestamp()
                    .WithFooter(stupidTextService.GetRandomStupidText());
            if (!string.IsNullOrWhiteSpace(userData.ProfileImage) && string.IsNullOrWhiteSpace(builder.ThumbnailUrl))
            {
                builder.WithThumbnailUrl(userData.ProfileImage);
            }
        }

        private EileenUserData GetUserData(ulong userId)
        {
            Write($"Retrieving UserData for {userId} from cache (aka NOT going to RavenDB)");
            if(!userContent.TryGetValue(userId, out var d))
            {
                throw new ArgumentException(nameof(userId));
            }
            return d;
        }

        private async Task<EileenUserData> CreateUserContent(ulong userId)
        {
            Write($"Creating new UserData for {userId}");
            var userData = new EileenUserData { UserId = userId };
            using(var session = ravenDatabaseService.GetOrAddDocumentStore("erector_users").OpenAsyncSession())
            {
                await session.StoreAsync(userData, userId.ToString());
                await session.SaveChangesAsync();
            }
            return userData;
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(UserService), message));
        }

    }


}