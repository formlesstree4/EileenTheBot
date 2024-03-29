using System;

namespace Bot.Models.Dungeoneering
{

    /// <summary>
    ///     Defines a piece of Equipment that can be worn
    /// </summary>
    public class Equipment : Item
    {

        /// <summary>
        ///     Gets or sets the type of Equipment (like a sword, spear, etc.)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        ///     Gets or sets the attack power of the Equipment
        /// </summary>
        public int AttackPower { get; set; }

        /// <summary>
        ///     Gets or sets an overridable price for this piece of Equipment
        /// </summary>
        public int? Price { get; set; } = null;

        /// <summary>
        ///     Gets or sets the location the equipment can be equipped
        /// </summary>
        public string Location { get; set; } = "";

        /// <summary>
        ///     Gets the value that this piece of equipment can be sold for
        /// </summary>
        public override int GetSellValue() => Price ?? (int)Math.Min(1, Math.Floor(AttackPower * 1.5));

        /// <summary>
        ///     Returns a nice string representation of the Equipment
        /// </summary>
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return $"{Type} (+{AttackPower:N0})";
            }
            return $"{Name} (+{AttackPower:N0})";
        }

    }


}