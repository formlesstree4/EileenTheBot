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
        private readonly Random _rng = new Random();
        private readonly UserService userService;
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly Func<LogMessage, Task> logger;
        private readonly ConcurrentDictionary<ulong, Encounter> currentEncounters;

        public DungeoneeringMainService(
            UserService userService,
            RavenDatabaseService ravenDatabaseService,
            Func<LogMessage, Task> logger)
        {
            this.userService = userService ?? throw new System.ArgumentNullException(nameof(userService));
            this.ravenDatabaseService = ravenDatabaseService ?? throw new ArgumentNullException(nameof(ravenDatabaseService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                var activeEncounters = await session.LoadAsync<Dictionary<ulong, Encounter>>("encounters");
                foreach(var encounter in activeEncounters)
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
                await session.StoreAsync(this.currentEncounters.ToDictionary(t => t.Key, t => t.Value), "encounters");
                await session.SaveChangesAsync();
            }
            Write("Dungeoneering Data Saved.");
        }

        public async Task<bool> IsUserRegistered(ulong userId)
        {
            var userData = await userService.GetOrCreateUserData(userId);
            return userData.HasTagData(TagName);
        }

        public async Task<PlayerCard> GetPlayerCard(ulong userId)
            => (await userService.GetOrCreateUserData(userId)).GetTagData<PlayerCard>(TagName);

        public async Task<PlayerCard> RegisterPlayer(ulong userId)
        {
            var userData = (await userService.GetOrCreateUserData(userId));
            var playerCard = new PlayerCard
            {
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


        public async Task<Encounter> CreateEncounter(ulong userId, ulong channelId)
        {
            var playerCard = await GetPlayerCard(userId);
            var monster = await CreateMonster(playerCard);
            var encounter = new Encounter
            {
                ActiveMonster = monster,
                ChannelId = channelId,
                Loot = await CreateAcceptableLoot(monster, playerCard),
                PlayerId = userId
            };
            return encounter;
        }


        private async Task<Monster> CreateMonster(PlayerCard player)
        {

            /*
                I need to figure this out still
            */
            throw new NotImplementedException();
        }

        private async Task<IEnumerable<Item>> CreateAcceptableLoot(Monster monster, PlayerCard player)
        {
            throw new NotImplementedException();
        }

        private Task<Embed> CreateDungeoneeringProfilePage(EileenUserData userData, IUser user)
        {
            return Task.FromResult(new EmbedBuilder().Build());
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(DungeoneeringMainService), message));
        }

        
        private Races GetRandomRace()
        {
            var v = Enum.GetValues (typeof (Races));
            return (Races) v.GetValue (_rng.Next(v.Length));
        }

    }

}