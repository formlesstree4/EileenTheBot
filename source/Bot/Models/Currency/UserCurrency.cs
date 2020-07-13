namespace Bot.Models.Currency
{
    public class UserCurrency : IUserCurrency
    {

        public long PlayerId { get; set; }

        public long ServerId { get; set; }

        public decimal Currency { get; set; }

        public void AddCurrency(decimal value)
        {
            throw new System.NotImplementedException();
        }

        public void ClearCurrency()
        {
            throw new System.NotImplementedException();
        }

        public void RemoveCurrency(decimal value)
        {
            throw new System.NotImplementedException();
        }
    }
}