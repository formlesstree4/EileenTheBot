namespace Bot.Models.Currency
{


    public sealed class EileenCurrencyData
    {

        public ulong Currency { get; set; }

        public ulong PassiveCurrencyCap { get; set; }

        public ulong MaxCurrency { get; set; }

        public byte Level { get; set; }

        public int Prestige { get; set; }

    }

}