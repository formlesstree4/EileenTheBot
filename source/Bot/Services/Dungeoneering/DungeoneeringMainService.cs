using Bot.Models.Dungeoneering;
using Bot.Models.Eileen;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Services.Dungeoneering
{

    [Summary("The main service for the Dungeoneering minigame. Primarily responsible for state management of the User details and Battles (both ongoing and historical)")]
    public sealed class DungeoneeringMainService : IEileenService
    {
        private const string TagName = "dungeoneering";
        private readonly Random random;
        private readonly UserService userService;
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly MonsterService monsterService;
        private readonly EquipmentService equipmentService;
        private readonly ILogger<DungeoneeringMainService> logger;
        private readonly ConcurrentDictionary<ulong, Encounter> currentEncounters;

        public DungeoneeringMainService(
            UserService userService,
            RavenDatabaseService ravenDatabaseService,
            MonsterService monsterService,
            EquipmentService equipmentService,
            ILogger<DungeoneeringMainService> logger,
            Random random)
        {
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.ravenDatabaseService = ravenDatabaseService ?? throw new ArgumentNullException(nameof(ravenDatabaseService));
            this.monsterService = monsterService ?? throw new ArgumentNullException(nameof(monsterService));
            this.equipmentService = equipmentService ?? throw new ArgumentNullException(nameof(equipmentService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.currentEncounters = new ConcurrentDictionary<ulong, Encounter>();
        }


        public async Task InitializeService()
        {
            logger.LogInformation("Initializing...");
            userService.RegisterProfileCallback(CreateDungeoneeringProfilePage);
            logger.LogInformation("Loading Prior Encounters...");
            await LoadServiceAsync();
            await Task.Yield();
        }

        public async Task LoadServiceAsync()
        {
            logger.LogInformation("Loading Dungeoneering Data...");
            var documentStore = ravenDatabaseService.GetOrAddDocumentStore("erector_dungeoneering");
            using (var session = documentStore.OpenAsyncSession())
            {
                logger.LogInformation("Retreiving previously active encounters");
                var activeEncounters = await session.LoadAsync<EncounterStorage>("encounters");
                if (activeEncounters is null)
                {
                    logger.LogInformation("No active encounters recorded, or the entry hasn't existed yet");
                    return;
                }
                foreach (var encounter in activeEncounters.ExistingEncounters)
                {
                    logger.LogInformation("Attempting to reload encounter for Channel {channel}", encounter.Key);
                    currentEncounters.TryAdd(encounter.Key, encounter.Value);
                }
                logger.LogInformation("Completed. Loaded {encounters} encounter(s)", currentEncounters.Count);
            }
            logger.LogInformation("Dungeoneering Data Loaded.");
        }

        public async Task SaveServiceAsync()
        {
            logger.LogInformation("Saving Dungeoneering Data...");
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
            logger.LogInformation("Dungeoneering Data Saved.");
        }


        public async Task<bool> IsUserRegisteredAsync(IUser user)
            => await IsUserRegisteredAsync(user.Id);

        public async Task<bool> IsUserRegisteredAsync(ulong userId)
        {
            logger.LogInformation("Validating if {userId} is registered...", userId);
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
            logger.LogInformation("Registering {userId} with dungoneer", userId);
            var userData = (await userService.GetOrCreateUserData(userId));
            var playerCard = new PlayerCard
            {
                AttackPower = 2,
                Battles = new List<BattleLog>(),
                Defeats = 0,
                Gear = new List<Equipment>(),
                IsConfirmed = false,
                Race = GetRandomRace().ToString(),
                Victories = 0
            };
            userData.SetTagData(TagName, playerCard);
            return playerCard;
        }


        public async Task<Encounter> CreateEncounterAsync(IUser user, IChannel channel)
            => await CreateEncounterAsync(user.Id, channel.Id);

        public async Task<Encounter> CreateEncounterAsync(ulong userId, ulong channelId)
        {
            logger.LogInformation("Creating an encounter in room {channelId} for {userId}", channelId, userId);
            var playerCard = await GetPlayerCardAsync(userId);
            var monster = await CreateMonster(playerCard);
            var encounter = new Encounter
            {
                ActiveMonster = monster,
                ChannelId = channelId,
                Loot = (await CreateAcceptableLoot(monster, playerCard)).ToList(),
                PlayerId = userId
            };
            currentEncounters.TryAdd(channelId, encounter);
            return encounter;
        }


        public async Task<Encounter> GetEncounterAsync(IChannel channel)
            => await GetEncounterAsync(channel.Id);

        public async Task<Encounter> GetEncounterAsync(ulong channelId)
            => await Task.FromResult(currentEncounters.TryGetValue(channelId, out var e) ? e : null);

        public async Task<ProfileCallback> CreateDungeoneeringProfilePage(ProfileCallback profileCallback)
        {
            var userData = profileCallback.UserData;
            var user = profileCallback.CurrentUser;
            var isRegistered = await IsUserRegisteredAsync(profileCallback.CurrentUser);
            PlayerCard dungeoneerData;

            if (!isRegistered)
            {
                dungeoneerData = new PlayerCard
                {
                    AttackPower = 0,
                    Defeats = 0,
                    Description = "_Not Registered_",
                    Race = "_Unknown_",
                    Victories = 0,
                    IsConfirmed = false,
                    Gear = null,
                    Battles = null
                };
            }
            else
            {
                dungeoneerData = userData.GetTagData<PlayerCard>(TagName);
            }
            profileCallback.PageBuilder
                .WithTitle(isRegistered ? "Dungeoneering Card" : "Guest Pass")
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
            return await Task.FromResult(profileCallback);
        }


        public async Task<bool> IsUserInAnyEncounterAsync(IUser user)
            => await IsUserInAnyEncounterAsync(user.Id);

        public async Task<bool> IsUserInAnyEncounterAsync(ulong userId)
            => await Task.FromResult(currentEncounters.Values.Any(c => c.PlayerId == userId));


        public async Task HandleVictoryAsync(PlayerCard player, Encounter encounter)
        {
            logger.LogInformation($"A victory is being recorded!");
            var battleLog = await CreateBattleLogAsync(player, encounter, "Victory");
            player.Victories += 1;
            player.AttackPower += 1;
            player.Battles.Add(battleLog);
            currentEncounters.TryRemove(encounter.ChannelId, out var e);
            await Task.Yield();
        }

        public async Task HandleDefeatAsync(PlayerCard player, Encounter encounter)
        {
            logger.LogInformation($"A defeat is being recorded!");
            var battleLog = await CreateBattleLogAsync(player, encounter, "Defeat");
            player.Defeats += 1;
            player.AttackPower = (int)Math.Max(1, Math.Floor(player.AttackPower / (decimal)2));
            player.Battles.Add(battleLog);
            currentEncounters.TryRemove(encounter.ChannelId, out var e);
            await Task.Yield();
        }

        public async Task HandleFleeAsync(PlayerCard player, Encounter encounter)
        {
            logger.LogInformation($"A 'flee' is being recorded!");
            var battleLog = await CreateBattleLogAsync(player, encounter, "Defeat (Fled)");
            player.Defeats += 1;
            player.Battles.Add(battleLog);
            currentEncounters.TryRemove(encounter.ChannelId, out var e);
            await Task.Yield();
        }


        public async Task<List<PlayerCard>> GetPlayerCardsAsync(List<ulong> uid)
        {
            var playerCards = new List<PlayerCard>();
            foreach(var id in uid)
            {
                playerCards.Add(await GetPlayerCardAsync(id));
            }
            return await Task.FromResult(playerCards);
        }

        private async Task<BattleLog> CreateBattleLogAsync(PlayerCard player, Encounter encounter, string result)
        {
            var assistants = new List<PlayerCard>();
            var instigators = new List<PlayerCard>();

            foreach (var assistant in encounter.Assistants ?? new List<ulong>())
            {
                assistants.Add(await GetPlayerCardAsync(assistant));
            }

            foreach (var instigator in encounter.Instigators ?? new List<ulong>())
            {
                instigators.Add(await GetPlayerCardAsync(instigator));
            }

            var battleLog = new BattleLog
            {
                Assistants = assistants,
                ChannelId = encounter.ChannelId,
                Instigators = instigators,
                MonsterFought = encounter.ActiveMonster,
                Player = player,
                Result = result
            };
            return battleLog;
        }

        private async Task<Monster> CreateMonster(PlayerCard player)
        {
            logger.LogTrace($"Creating a Monster");
            var level = player.GetActualPower();
            var monster =
                await monsterService.CreateMonsterAsync(level) ??
                await monsterService.CreateMonsterFromRange(Math.Max(1, level - 1), level + 1);
            return monster;
        }

        private async Task<IEnumerable<Item>> CreateAcceptableLoot(Monster monster, PlayerCard player)
        {
            var level = player.GetActualPower();
            var loot = equipmentService.GetEquipmentInRange(Math.Max(1, level - 1), level + 1).Take(random.Next(2));
            return await Task.FromResult(loot.Select(c => c.ToEquipment()));
        }

        private Races GetRandomRace()
        {
            var v = Enum.GetValues(typeof(Races));
            return (Races)v.GetValue(SafeNext(v.Length));
        }

        private int SafeNext(int min, int max)
        {
            lock (random)
            {
                return random.Next(min, max);
            }
        }

        private int SafeNext(int max)
        {
            lock (random)
            {
                return random.Next(max);
            }
        }

    }

}
