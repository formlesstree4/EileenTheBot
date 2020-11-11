using Hangfire;
using Hangfire.Raven.Storage;
using Raven.Client.Documents;

namespace Bot.Services.RavenDB
{
    public sealed class RavenDatabaseService
    {

        private readonly string RavenDBLocation;
        private readonly IDocumentStore documentStore;

        public RavenDatabaseService()
        {
            RavenDBLocation = System.Environment.GetEnvironmentVariable("RavenIP");
        }


        public string GetConnectionStringWithDatabase(string dbName)
        {
            return $"{RavenDBLocation};Database={dbName}";
        }

        public string GetConnectionString => RavenDBLocation;


        public void InitializeService()
        {
            GlobalConfiguration.Configuration.UseRavenStorage(RavenDBLocation, "erector_hangfire");
        }

    }
}