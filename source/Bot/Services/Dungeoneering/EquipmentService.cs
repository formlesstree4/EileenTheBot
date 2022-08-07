using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models.Dungeoneering.Special.Equipment;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;

namespace Bot.Services.Dungeoneering
{

    [Summary("An incomplete supporting service that will allow for equipment to be randomly created as both loot and Monster equipment")]
    public sealed class EquipmentService : IEileenService
    {


        public ReadOnlyCollection<WrappedEquipment> Equipment { get; private set; }

        public ReadOnlyCollection<WrappedEquipment> Weapons =>
            Equipment.Where(c => c.EquipmentType.Equals("weapon", StringComparison.OrdinalIgnoreCase)).ToList().AsReadOnly();

        public ReadOnlyCollection<WrappedEquipment> Armor =>
            Equipment.Where(c => c.EquipmentType.Equals("armor", StringComparison.OrdinalIgnoreCase)).ToList().AsReadOnly();


        private readonly RavenDatabaseService ravenDatabaseService;
        private readonly ILogger<EquipmentService> logger;
        private readonly Random rng;

        public EquipmentService(
            RavenDatabaseService ravenDatabaseService,
            ILogger<EquipmentService> logger,
            Random rng
        )
        {
            this.ravenDatabaseService = ravenDatabaseService ?? throw new ArgumentNullException(nameof(ravenDatabaseService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }


        public async Task InitializeService()
        {
            logger.LogInformation("Initializing...");
            using (var session = ravenDatabaseService.GetOrAddDocumentStore("erector_dungeoneering").OpenAsyncSession())
            {
                logger.LogInformation("Loading Equipment Information...");
                var sourceData = await session.LoadAsync<EquipmentCollection>("equipment");
                this.Equipment = sourceData.GetEquipment().ToList().AsReadOnly();
                logger.LogInformation("Successfully loaded {equipment} equipment", Equipment.Count.ToString("N0"));
            }
            logger.LogInformation("Initialized...");
            await Task.Yield();
        }

        public IEnumerable<WrappedEquipment> GetEquipmentInRange(int minLevel, int maxLevel) =>
            GetWeaponInRange(minLevel, maxLevel)
                .Union(GetArmorInRange(minLevel, maxLevel));

        public IEnumerable<WrappedEquipment> GetWeaponInRange(int minLevel, int maxLevel) =>
            GetEquipmentTypeInRange(minLevel, maxLevel, "weapon");

        public IEnumerable<WrappedEquipment> GetArmorInRange(int minLevel, int maxLevel) => 
            GetEquipmentTypeInRange(minLevel, maxLevel, "armor");

        public IEnumerable<string> GetEquipmentLocations() =>
            Equipment.Select(c => c.EquipLocation).Distinct(StringComparer.OrdinalIgnoreCase);

        private IEnumerable<WrappedEquipment> GetEquipmentTypeInRange(int minLevel, int maxLevel, string type)
        {
            return from e in Equipment
                   let we = e
                   where we.EquipmentLevel != null &&
                        we.EquipmentLevel >= minLevel &&
                        we.EquipmentLevel <= maxLevel &&
                        we.EquipmentType.Equals(type, StringComparison.OrdinalIgnoreCase)
                   select we;
        }

    }
}
