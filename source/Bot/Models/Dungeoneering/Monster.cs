using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.Dungeoneering
{


    public sealed class Monster
    {

        /// <summary>
        ///     Gets or sets the name of the Monster
        /// </summary>
        public string Name { get; set; }

        /// </summary>
        ///     Gets or sets the monster level of the Monster
        /// </summary>
        /// <value></value>
        public int MonsterLevel { get; set; }

        /// <summary>
        ///     Gets or sets the monster level of the Monster
        /// </summary>
        /// <value></value>
        public int MonsterLevel { get; set; }

        /// <summary>
        ///     Gets or sets the theoretical power of the Monster
        /// </summary>
        /// <value></value>
        public int TheoreticalPower { get; set; }

        /// <summary>
        ///     Gets or sets a collection of equipment attached to this Monster
        /// </summary>
        /// <value></value>
        public IEnumerable<Equipment> Equipment { get; set; }

        /// <summary>
        ///     Gets the actual attack power of the Monster
        /// </summary>
        public int GetActualPower()
        {
            return TheoreticalPower + (Equipment?.Sum(f => f.AttackPower) ?? 0);
        }

    }


}
