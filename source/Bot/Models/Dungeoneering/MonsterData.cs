using System.Collections.Generic;

namespace Bot.Models.Dungeoneering
{

    /// <summary>
    ///     Defines the object structure of the raw Monster Data from Raven DB
    /// </summary>
    public sealed class MonsterData
    {

        /// <summary>
        ///     Gets or sets the name of the Monster
        /// </summary>
        public string Monster { get; set; }

        /// <summary>
        ///     Gets or sets the base level(s) of the Monster
        /// </summary>
        public IEnumerable<int> Levels { get; set; }

    }


    /// <summary>
    ///     Defines the data stored in RavenDB for all the Monsters
    /// </summary>
    public sealed class MonsterDocument
    {

        /// <summary>
        ///     Gets or sets a collection of MonsterData entries
        /// </summary>
        public IEnumerable<MonsterData> Monsters { get; set; }

    }

}