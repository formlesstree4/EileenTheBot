namespace Bot.Models.Dungeoneering
{


    /// <summary>
    ///     This does nothing currently and will not affect battles. It's 99% for flavor right now.
    /// </summary>
    public enum Races
    {

        /// <summary>
        ///     Run-of-the-mill plain human race.
        ///     The stats are below average but all equipment not specially crafted
        ///     for a race can be used by humans.
        /// </summary>
        Human,

        /// <summary>
        ///     The elegant and never-aging elves.
        ///     Their stats in archery and magic are above average however their
        ///     others fall a bit short.
        /// </summary>
        Elf,

        /// <summary>
        ///     Brutish and powerful. Orcs don't have the ability to use high dexterous weapons or magicks
        ///     but their strength, vitality, and endurance far and beyond make up for it.
        /// </summary>
        Orc,
        
        /// <summary>
        ///     Highly intelligent, magical creatures with absurd strength. However their lack of a faith
        ///     means that any religious individual from the other races can potentially
        ///     handle their shenanigans with ease.
        /// </summary>
        Demon,

    }


}