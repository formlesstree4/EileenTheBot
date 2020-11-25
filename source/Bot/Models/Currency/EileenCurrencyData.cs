using System;

namespace Bot.Models.Currency
{


    public sealed class EileenCurrencyData
    {

        /// <summary>
        ///     Gets or sets the current currency cap.
        /// </summary>
        /// <value></value>
        public ulong Currency { get; set; }

        /// <summary>
        ///     Gets or sets the cut-off for passive currency gain.
        /// </summary>
        /// <value></value>
        public ulong PassiveCurrencyCap { get; set; }

        /// <summary>
        ///     Gets or sets the minimum currency necessary to jump to the next level.
        /// </summary>
        /// <value></value>
        public ulong MaxCurrency { get; set; }

        /// <summary>
        ///     Gets or sets the current level.
        /// </summary>
        /// <value></value>
        public byte Level { get; set; }

        /// <summary>
        ///     Gets or sets the current Prestige.
        /// </summary>
        /// <value></value>
        public int Prestige { get; set; }

        /// <summary>
        ///     Gets or sets the last time a claim has happened for 'daily currency'.
        /// </summary>
        /// <value></value>
        public DateTime? DailyClaim { get; set; }

    }

}