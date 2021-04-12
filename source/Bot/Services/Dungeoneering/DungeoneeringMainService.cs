using Bot.Models;
using Bot.Models.Dungeoneering;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
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
        private readonly Func<LogMessage, Task> logger;
        private readonly ConcurrentDictionary<ulong, Encounter> currentEncounters;

        public DungeoneeringMainService(
            UserService userService,
            RavenDatabaseService ravenDatabaseService,
            MonsterService monsterService,
            EquipmentService equipmentService,
            Func<LogMessage, Task> logger,
            Random random)
        {
            this.userService = userService ?? throw new System.ArgumentNullException(nameof(userService));
            this.ravenDatabaseService = ravenDatabaseService ?? throw new ArgumentNullException(nameof(ravenDatabaseService));
            this.monsterService = monsterService ?? throw new ArgumentNullException(nameof(monsterService));
            this.equipmentService = equipmentService ?? throw new ArgumentNullException(nameof(equipmentService));
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
                foreach (var encounter in activeEncounters.ExistingEncounters)
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
            Write($"Creating an encounter in room {channelId} for {userId}");
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
            Write($"A victory is being recorded!");
            var battleLog = await CreateBattleLogAsync(player, encounter, "Victory");
            player.Victories += 1;
            player.AttackPower += 1;
            player.Battles.Add(battleLog);

            foreach (Item i in encounter.Loot)
            {
                player.Inventory.Add(i);
            }

            currentEncounters.TryRemove(encounter.ChannelId, out var e);
            await Task.Yield();
        }

        public async Task HandleDefeatAsync(PlayerCard player, Encounter encounter)
        {
            Write($"A defeat is being recorded!");
            var battleLog = await CreateBattleLogAsync(player, encounter, "Defeat");
            player.Defeats += 1;
            player.AttackPower = (int)Math.Max(1, Math.Floor(player.AttackPower / (decimal)2));
            player.Battles.Add(battleLog);
            currentEncounters.TryRemove(encounter.ChannelId, out var e);
            await Task.Yield();
        }

        public async Task HandleFleeAsync(PlayerCard player, Encounter encounter)
        {
            Write($"A 'flee' is being recorded!");
            var battleLog = await CreateBattleLogAsync(player, encounter, "Defeat (Fled)");
            player.Defeats += 1;
            ///so this function is called on a SUCCESSFUL flee, but still adds to the defeat counter...
            ///I can see the thought process behind this, and I wouldn't neccessarily argue for counting it as a victory instead, BUT...
            ///if my little level 10 human gets away from a level 9000 Goku I wouldn't be unhappy with that result or call it a defeat (particularly since the player doesn't die or lose anything)
            ///I feel it kind've discourages you from fleeing at all, even if you know you'll probably lose the fight, since it gets counted as a defeat regardless of whether you get away or not.
            ///It makes a bit more sense to me to just not mark it down as either a win nor loss, at least on the visible playercard, but this is ultimately down to you
            ///just wanted to share my thoughts on that, since we had mentioned it earlier :)
            
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

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(DungeoneeringMainService), message));
        }

        private async Task<Monster> CreateMonster(PlayerCard player)
        {
            Write($"Creating a Monster", LogSeverity.Verbose);
            var level = player.GetActualPower();
            ///This sets the level of the monster to the players attack power, WITH GEAR INCLUDED.
            ///Meaning equipping improved gear will spawn equally improved monsters...
            ///...thus not *actually* increasing the player's power and resulting in gear being semi-useless?
            ///This also makes it more beneficial to unequip some gear, spawn the monster, re-equip, and then fight,
            ///...ensuring that the monster is always weaker than the player.
            ///This is potentially abuse-able and kind've un-fun, and it would probably be better to base...
            ///...the monster's level off of the player's level/base attack power.
            ///
            ///also noticed there isn't actually a "player level" variable. This would be useful I think :)

            var monster =
                await monsterService.CreateMonsterAsync(level) ??
                await monsterService.CreateMonsterFromRange(Math.Max(1, level - 1), level + 1);
            return monster;
        }

        private async Task<IEnumerable<Item>> CreateAcceptableLoot(Monster monster, PlayerCard player)
        {
            var level = player.GetActualPower();
            var loot = equipmentService.GetEquipmentInRange(Math.Max(1, level - 1), level + 1);
            return await Task.FromResult(loot.Select(c => c.ToEquipment())); ///Don't know what this line does really, double check that it's necessary with the new GetEquipmentTypeInRange function! 
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
