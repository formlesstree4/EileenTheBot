namespace Bot.Models.Dungeoneering
{

    /// <summary>
    ///     Defines the base level 'item' class
    /// </summary>
    public class Item
    {

        /// <summary>
        ///     Gets or sets the name of this Item
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Gets or sets a base value for this Item
        /// </summary>
        /// <value></value>
        public int BaseValue { get; set; }

        /// <summary>
        ///     Gets the value that this item can be sold for
        /// </summary>
        /// <returns></returns>
        public virtual int GetSellValue() => BaseValue;

        /// <summary>
        ///     Returns a string representation of the Item
        /// </summary>
        public override string ToString() => Name;

    }


}