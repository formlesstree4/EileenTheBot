using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.Dungeoneering.Special.Equipment
{

    /// <summary>
    ///     Defines a collection of equipment data stored inside RavenDB
    /// </summary>
    public sealed class EquipmentCollection
    {

        /// <summary>
        ///     Gets or sets the collection of <see cref="JObject"/> definitions for all weapons in the DocumentDB
        /// </summary>
        public List<JObject> Equipment { get; set; }



        /// <summary>
        ///     Finds pieces of equipment in range
        /// </summary>
        /// <param name="minLevel">The lower level</param>
        /// <param name="maxLevel">The upper level</param>
        /// <returns>A collection of <see cref="WrappedEquipment"/></returns>
        public IEnumerable<WrappedEquipment> GetEquipmentInRange(int minLevel, int maxLevel)
        {
            return from e in Equipment
                   let we = new WrappedEquipment(e)
                   where we.EquipmentLevel != null && we.EquipmentLevel >= minLevel && we.EquipmentLevel <= maxLevel
                   select we;
        }

        /// <summary>
        ///     Wraps all equipment in the <see cref="WrappedEquipment"/> class which exposes properties
        /// </summary>
        /// <returns>A collection of <see cref="WrappedEquipment"/></returns>
        public IEnumerable<WrappedEquipment> GetEquipment() =>
            from e in Equipment
            select new WrappedEquipment(e);

        /// <summary>
        ///     Returns a <see cref="WrappedEquipment"/> class based on the Weapon ID
        /// </summary>
        /// <param name="id">The ID of the weapon</param>
        /// <returns><see cref="WrappedEquipment"/></returns>
        public WrappedEquipment GetWeaponById(int id)
        {
            return (from e in Equipment
                    let we = new WrappedEquipment(e)
                    where we.EquipmentId == id
                    select we).First();
        }

    }


    public sealed class WrappedEquipment
    {

        private readonly JObject backingData;


        /// <summary>
        ///     Gets the Equipment ID
        /// </summary>
        /// <value>An integer value that uniquely identifies the enemy</value>
        public int EquipmentId { get; private set; }

        /// <summary>
        ///     Gets the name of the Equipment
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        ///     gets the minimum level that this Equipment can be equipped
        /// </summary>
        public int? EquipmentLevel { get; private set; }

        /// <summary>
        ///     Gets the TYPE of Equipment
        /// </summary>
        public string EquipmentType { get; private set; }

        /// <summary>
        ///     Gets the location of where it can be equipped
        /// </summary>
        public string EquipLocation { get; private set; }

        /// <summary>
        ///     Gets the attack power of this Equipment
        /// </summary>
        /// <value></value>
        public int Power { get; private set; }

        /// <summary>
        ///     Gets the VALUE of this Equipment
        /// </summary>
        public int Value { get; private set; }



        public WrappedEquipment(JObject sourceData)
        {
            backingData = sourceData;
            ReadObject();
        }

        private void ReadObject()
        {
            var name = backingData["Name"];
            var weaponId = backingData["Id"];
            var type = backingData["Type"];
            var level = backingData["EquipLevelMin"] ?? backingData["WeaponLevel"] ?? backingData["Defense"];
            var power = backingData["Attack"];
            var price = backingData["Buy"];

            EquipmentId = weaponId.Value<int?>() ?? -1;
            Name = name?.Value<string>() ?? "";
            EquipmentLevel = level?.Value<int?>() ?? 0;
            EquipmentType = type?.Value<string>() ?? "";
            EquipLocation = GetEquipmentLocation(backingData["Locations"]);
            Power = (int)Math.Max(1, Math.Floor((power?.Value<int?>() ?? 0.0f) / 10));
            Value = price?.Value<int?>() ?? (int)Math.Floor(Power * 1.5f);
        }

        private string GetEquipmentLocation(JToken location)
        {
            if (location is null) return "";
            if (!location.HasValues) return "";
            foreach (var c in location.Values<JProperty>())
            {
                var name = c.Name;
                var canEquip = c?.First?.Value<bool>() ?? false;
                if (canEquip) return name;
            }
            return "";
        }

        public Dungeoneering.Equipment ToEquipment()
        {
            return new Dungeoneering.Equipment
            {
                AttackPower = this.EquipmentLevel ?? 1,
                BaseValue = this.Value,
                Name = this.Name,
                Location = this.EquipLocation,
                Price = this.Power,
                Type = this.EquipmentType
            };
        }

    }

}
