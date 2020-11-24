using System;
using System.Threading.Tasks;
using Bot.Services.RavenDB;
using Discord;


namespace Bot.Services
{

    public sealed class CurrencyService
    {
        private readonly RavenDatabaseService rdbs;
        private readonly Func<LogMessage, Task> logger;


        public CurrencyService(RavenDatabaseService rdbs, Func<LogMessage, Task> logger)
        {
            this.rdbs = rdbs;
            this.logger = logger;
        }


        public async Task InitializeService()
        {
            await Task.Yield();
        }

        public async Task UpdateUserCurrency()
        {
            await Task.Yield();
        }





        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(CurrencyService), message));
        }

    }


}