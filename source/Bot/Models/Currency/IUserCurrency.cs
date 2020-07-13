namespace Bot.Models.Currency
{
    public interface IUserCurrency
    {
         long PlayerId { get; }
         long ServerId { get; }
         decimal Currency { get; }


        void AddCurrency(decimal value);

        void RemoveCurrency(decimal value);

        void ClearCurrency();

    }
}