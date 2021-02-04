using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

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
                   where we.WeaponLevel != null && we.WeaponLevel >= minLevel && we.WeaponLevel <= maxLevel
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
                    where we.WeaponId == id
                    select we).First();
        }

    }


    public sealed class WrappedEquipment
    {

        private readonly JObject backingData;


        /// <summary>
        ///     Gets the Weapon ID
        /// </summary>
        /// <value>An integer value that uniquely identifies the enemy</value>
        public int WeaponId { get; private set; }

        /// <summary>
        ///     Gets the name of the weapon
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        ///     gets the minimum level that this weapon can be equipped
        /// </summary>
        public int? WeaponLevel { get; private set; }

        /// <summary>
        ///     Gets the TYPE of weapon
        /// </summary>
        public string WeaponType { get; private set; }

        /// <summary>
        ///     Gets the location of where it can be equipped
        /// </summary>
        public string EquipLocation { get; private set; }

        /// <summary>
        ///     Gets the attack power of this weapon
        /// </summary>
        /// <value></value>
        public int AttackPower { get; private set; }

        /// <summary>
        ///     Gets the VALUE of this weapon
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
            var level = backingData["EquipLevelMin"] ?? backingData["WeaponLevel"];
            var power = backingData["Attack"];
            var price = backingData["Buy"];

            WeaponId = weaponId.Value<int?>() ?? -1;
            Name = name.Value<string>() ?? "";
            WeaponLevel = level.Value<int?>() ?? 0;
            WeaponType = type.Value<string>() ?? "";
            EquipLocation = GetEquipmentLocation(backingData["Locations"]);
            AttackPower = (int)Math.Max(1, Math.Floor((power.Value<int?>() ?? 0.0f) / 10));
            Value = price.Value<int?>() ?? (int)Math.Floor(AttackPower * 1.5f);
        }

        private string GetEquipmentLocation(JToken location)
        {
            if (!location.HasValues) return "";
            foreach(var c in location.Values<JProperty>())
            {
                var name = c.Name;
                var canEquip = c?.First?.Value<bool>() ?? false;
                if (canEquip) return name;
            }
            return "";
        }

        public Models.Dungeoneering.Equipment ToEquipment()
        {
            return new Dungeoneering.Equipment
            {
                AttackPower = this.AttackPower,
                BaseValue = this.Value,
                Name = this.Name,
                Location = this.EquipLocation,
                Price = this.Value,
                Type = this.WeaponType
            };
        }

    }

}