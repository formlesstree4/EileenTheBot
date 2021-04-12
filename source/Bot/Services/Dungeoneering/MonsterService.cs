using Bot.Models.Dungeoneering;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Services.Dungeoneering
{

    [Summary("A supporting service for the Dungeoneering minigame that provides randomly generated Monster information, even allowing for a series of Monsters to be generated at a variety of level ranges")]
    public sealed class MonsterService : IEileenService
    {
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly EquipmentService equipmentService;
        private readonly Random random;
        private readonly Func<LogMessage, Task> logger;
        private readonly List<MonsterData> monsters;

        public MonsterService(
            RavenDatabaseService ravenDatabaseService,
            EquipmentService equipmentService,
            Func<LogMessage, Task> logger,
            Random rng)
        {
            this.ravenDatabaseService = ravenDatabaseService ?? throw new System.ArgumentNullException(nameof(ravenDatabaseService));
            this.equipmentService = equipmentService ?? throw new ArgumentNullException(nameof(equipmentService));
            this.random = rng ?? throw new ArgumentNullException(nameof(rng));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.monsters = new List<MonsterData>();
        }



        public async Task InitializeService()
        {
            Write("Initializing...");
            using (var session = ravenDatabaseService.GetOrAddDocumentStore("erector_dungeoneering").OpenAsyncSession())
            {
                Write("Loading Monster Information...");
                var sourceData = await session.LoadAsync<MonsterDocument>("monsterData");
                this.monsters.AddRange(sourceData.Monsters);
                Write($"Successfully loaded {this.monsters.Count} monsters");
            }
            Write("Initialized");
            await Task.Yield();
        }

        public async Task<Monster> CreateMonsterAsync(int level)
        {
            var eligibleMonsters = (from m in monsters
                                    where m.Levels.Contains(level)
                                    select m).ToList();
            var selectedMonster = eligibleMonsters[random.Next(eligibleMonsters.Count)];
            var monsterLevel = Math.Max(1, random.Next(level - 3, level));
            var monster = new Monster
            {
                Equipment = GetMonsterEquipment(level),
                Name = selectedMonster.Monster,
                MonsterLevel = monsterLevel,
                TheoreticalPower = monsterLevel
            };
            return await Task.FromResult(monster);
        }

        public async Task<Monster> CreateMonsterFromRange(int min, int max)
        {
            var eligibleMonsters = (from m in monsters
                                    where m.Levels.Any(c => c >= min) &&
                                          m.Levels.Any(c => c <= max)
                                    select m).ToList();
            var selectedMonster = eligibleMonsters[random.Next(eligibleMonsters.Count)];
            var monsterLevel = Math.Max(1, random.Next(min, max));
            var monster = new Monster
            {
                Equipment = GetMonsterEquipment(monsterLevel),
                Name = selectedMonster.Monster,
                MonsterLevel = monsterLevel,
                TheoreticalPower = monsterLevel
            };
            return await Task.FromResult(monster);
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(MonsterService), message));
        }

        private List<Equipment> GetMonsterEquipment(int level)
        {
            var hasEquipment = random.Next(100) <= level;
            // for now weapons only
            // eventually we'll bust out into armor
            var equipment = new List<Equipment>();
            if (hasEquipment)
            {
                var weapon = equipmentService.GetWeaponInRange(Math.Max(1, level - 3), level).FirstOrDefault();
                equipment.Add(weapon.ToEquipment());
            }
            return equipment;
        }


    }


}
