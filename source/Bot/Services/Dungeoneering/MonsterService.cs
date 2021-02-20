using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models.Dungeoneering;
using Bot.Services.RavenDB;
using Discord;

namespace Bot.Services.Dungeoneering
{

    public sealed class MonsterService : IEileenService
    {
        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly Random random;
        private readonly Func<LogMessage, Task> logger;
        private readonly List<MonsterData> monsters;

        public MonsterService(
            RavenDatabaseService ravenDatabaseService,
            Func<LogMessage, Task> logger,
            Random rng)
        {
            this.ravenDatabaseService = ravenDatabaseService ?? throw new System.ArgumentNullException(nameof(ravenDatabaseService));
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
            var monster = new Monster
            {
                Equipment = new List<Equipment>(),
                Name = selectedMonster.Monster,
                TheoreticalPower = Math.Max(1, random.Next(level - 3, level))
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
            var monster = new Monster
            {
                Equipment = new List<Equipment>(),
                Name = selectedMonster.Monster,
                TheoreticalPower = Math.Max(1, random.Next(min, max))
            };
            return await Task.FromResult(monster);
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(MonsterService), message));
        }

    }


}