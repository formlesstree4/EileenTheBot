using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models;
using Bot.Models.Dungeoneering;
using Bot.Services.RavenDB;
using Discord;

namespace Bot.Services.Dungeoneering
{

    public sealed class DungeoneeringMainService
    {
        private const string TagName = "dungeoneering";
        private readonly Random random;
        private readonly UserService userService;
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly MonsterService monsterService;
        private readonly Func<LogMessage, Task> logger;
        private readonly ConcurrentDictionary<ulong, Encounter> currentEncounters;

        public DungeoneeringMainService(
            UserService userService,
            RavenDatabaseService ravenDatabaseService,
            MonsterService monsterService,
            Func<LogMessage, Task> logger,
            Random random)
        {
            this.userService = userService ?? throw new System.ArgumentNullException(nameof(userService));
            this.ravenDatabaseService = ravenDatabaseService ?? throw new ArgumentNullException(nameof(ravenDatabaseService));
            this.monsterService = monsterService ?? throw new ArgumentNullException(nameof(monsterService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.currentEncounters = new ConcurrentDictionary<ulong, Encounter>();
        }


        public async Task InitializeService()
        {
            Write("Initializing...");
            userService.RegisterProfileCallback(CreateDungeoneeringProfilePage);
            Write("Loading Prior Encounters...");
            await LoadServiceAsync();
            await Task.Yield();
        }

        public async Task LoadServiceAsync()
        {
            Write("Loading Dungeoneering Data...");
            var documentStore = ravenDatabaseService.GetOrAddDocumentStore("erector_dungeoneering");
            using (var session = documentStore.OpenAsyncSession())
            {
                Write("Retreiving previously active encounters");
                var activeEncounters = await session.LoadAsync<EncounterStorage>("encounters");
                if (activeEncounters is null)
                {
                    Write("No active encounters recorded, or the entry hasn't existed yet");
                    return;
                }
                foreach(var encounter in activeEncounters.ExistingEncounters)
                {
                    Write($"Attempting to reload encounter for Channel {encounter.Key}");
                    currentEncounters.TryAdd(encounter.Key, encounter.Value);
                }
                Write($"Completed. Loaded {currentEncounters.Count} encounter(s)");
            }
            Write("Dungeoneering Data Loaded.");
        }

        public async Task SaveServiceAsync()
        {
            Write("Saving Dungeoneering Data...");
            var documentStore = ravenDatabaseService.GetOrAddDocumentStore("erector_dungeoneering");
            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(
                    new EncounterStorage
                    {
                        ExistingEncounters = this.currentEncounters.ToDictionary(t => t.Key, t => t.Value)
                    }, "encounters");
                await session.SaveChangesAsync();
            }
            Write("Dungeoneering Data Saved.");
        }


        public async Task<bool> IsUserRegisteredAsync(IUser user)
            => await IsUserRegisteredAsync(user.Id);

        public async Task<bool> IsUserRegisteredAsync(ulong userId)
        {
            Write($"Validating if {userId} is registered...");
            var userData = await userService.GetOrCreateUserData(userId);
            return userData.HasTagData(TagName);
        }

        public async Task<PlayerCard> GetPlayerCardAsync(IUser user)
            => await GetPlayerCardAsync(user.Id);

        public async Task<PlayerCard> GetPlayerCardAsync(ulong userId)
            => (await userService.GetOrCreateUserData(userId)).GetTagData<PlayerCard>(TagName);

        public async Task<PlayerCard> RegisterPlayerAsync(IUser user)
            => await RegisterPlayerAsync(user.Id);

        public async Task<PlayerCard> RegisterPlayerAsync(ulong userId)
        {
            Write($"Registering {userId} with dungoneer");
            var userData = (await userService.GetOrCreateUserData(userId));
            var playerCard = new PlayerCard
            {
                AttackPower = 2,
                Battles = new List<BattleLog>(),
                Defeats = 0,
                Gear = new List<Equipment>(),
                IsConfirmed = false,
                Race = GetRandomRace(),
                Victories = 0
            };
            userData.SetTagData(TagName, playerCard);
            return playerCard;
        }


        public async Task<Encounter> CreateEncounterAsync(IUser user, IChannel channel)
            => await CreateEncounterAsync(user.Id, channel.Id);

        public async Task<Encounter> CreateEncounterAsync(ulong userId, ulong channelId)
        {
            Write($"Creating an encounter in room {channelId} for {userId}");
            var playerCard = await GetPlayerCardAsync(userId);
            var monster = await CreateMonster(playerCard);
            var encounter = new Encounter
            {
                ActiveMonster = monster,
                ChannelId = channelId,
                Loot = await CreateAcceptableLoot(monster, playerCard),
                PlayerId = userId
            };
            currentEncounters.TryAdd(channelId, encounter);
            return encounter;
        }


        public async Task<Encounter> GetEncounterAsync(IChannel channel)
            => await GetEncounterAsync(channel.Id);

        public async Task<Encounter> GetEncounterAsync(ulong channelId)
            => await Task.FromResult(currentEncounters.TryGetValue(channelId, out var e) ? e : null);

        public Task<Embed> CreateDungeoneeringProfilePage(EileenUserData userData, IUser user)
        {
            var dungeoneerData = userData.GetTagData<PlayerCard>(TagName);
            var builder = new EmbedBuilder()
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithName(user.Username)
                    .WithIconUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()))
                .WithColor(new Color(152, 201, 124))
                .WithCurrentTimestamp()
                .AddField(new EmbedFieldBuilder()
                        .WithName("Race")
                        .WithValue(dungeoneerData.Race.ToString())
                        .WithIsInline(true))
                .AddField(new EmbedFieldBuilder()
                    .WithName("Victories")
                    .WithValue(dungeoneerData.Victories.ToString("N0"))
                    .WithIsInline(true))
                .AddField(new EmbedFieldBuilder()
                    .WithName("Defeats")
                    .WithValue(dungeoneerData.Defeats.ToString("N0"))
                    .WithIsInline(true))
                .AddField(new EmbedFieldBuilder()
                    .WithName("Power")
                    .WithValue(dungeoneerData.GetActualPower().ToString("N0"))
                    .WithIsInline(true));
            return Task.FromResult(builder.Build());
        }


        public async Task<bool> IsUserInAnyEncounterAsync(IUser user)
            => await IsUserInAnyEncounterAsync(user.Id);

        public async Task<bool> IsUserInAnyEncounterAsync(ulong userId)
            => await Task.FromResult(currentEncounters.Values.Any(c => c.PlayerId == userId));


        public async Task HandleVictoryAsync(PlayerCard player, Encounter encounter)
        {
            Write($"A victory is being recorded!");
            var battleLog = new BattleLog
            {
                Assistants = Enumerable.Empty<PlayerCard>(),
                ChannelId = encounter.ChannelId,
                Instigators = Enumerable.Empty<PlayerCard>(),
                MonsterFought = encounter.ActiveMonster,
                Player = player,
                Result = "Victory"
            };
            player.Victories += 1;
            player.AttackPower += 1;
            player.Battles.Add(battleLog);
            currentEncounters.TryRemove(encounter.ChannelId, out var e);
            await Task.Yield();
        }

        public async Task HandleDefeatAsync(PlayerCard player, Encounter encounter)
        {
            Write($"A defeat is being recorded!");
            var battleLog = new BattleLog
            {
                Assistants = Enumerable.Empty<PlayerCard>(),
                ChannelId = encounter.ChannelId,
                Instigators = Enumerable.Empty<PlayerCard>(),
                MonsterFought = encounter.ActiveMonster,
                Player = player,
                Result = "Defeat"
            };
            player.Defeats += 1;
            player.AttackPower = (int)Math.Max(1, Math.Floor(player.AttackPower / (decimal)2));
            player.Battles.Add(battleLog);
            currentEncounters.TryRemove(encounter.ChannelId, out var e);
            await Task.Yield();
        }

        public async Task HandleFleeAsync(PlayerCard player, Encounter encounter)
        {
            Write($"A 'flee' is being recorded!");
            var battleLog = new BattleLog
            {
                Assistants = Enumerable.Empty<PlayerCard>(),
                ChannelId = encounter.ChannelId,
                Instigators = Enumerable.Empty<PlayerCard>(),
                MonsterFought = encounter.ActiveMonster,
                Player = player,
                Result = "Defeat (Fled)"
            };
            player.Defeats += 1;
            player.Battles.Add(battleLog);
            currentEncounters.TryRemove(encounter.ChannelId, out var e);
            await Task.Yield();
        }


        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(DungeoneeringMainService), message));
        }

        private async Task<Monster> CreateMonster(PlayerCard player)
        {
            Write($"Creating a Monster", LogSeverity.Verbose);
            var level = player.GetActualPower();
            var monster =
                await monsterService.CreateMonsterAsync(level) ??
                await monsterService.CreateMonsterFromRange(Math.Max(1, level - 10), level + 10);
            return monster;
        }

        private async Task<IEnumerable<Item>> CreateAcceptableLoot(Monster monster, PlayerCard player)
        {
            return await Task.FromResult(Enumerable.Empty<Item>());
        }

        private Races GetRandomRace()
        {
            var v = Enum.GetValues (typeof (Races));
            return (Races) v.GetValue (SafeNext(v.Length));
        }

        private int SafeNext(int min, int max)
        {
            lock(random)
            {
                return random.Next(min, max);
            }
        }

        private int SafeNext(int max)
        {
            lock(random)
            {
                return random.Next(max);
            }
        }

    }

}