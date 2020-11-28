using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models.Raven;
using Hangfire;
using Raven.Client.Documents;

namespace Bot.Services.RavenDB
{
    public sealed class RavenDatabaseService
    {

        private BotConfiguration configuration;
        private readonly string RavenDBLocation;
        private Lazy<IDocumentStore> coreDocumentStore;
        private Lazy<IDocumentStore> userDocumentStore;
        private Lazy<IDocumentStore> markovDocumentStore;
        private Lazy<IDocumentStore> cmdPermissionsStore;


        public IDocumentStore GetCoreConnection => coreDocumentStore.Value;

        public IDocumentStore GetUserConnection => userDocumentStore.Value;

        public IDocumentStore GetMarkovConnection => markovDocumentStore.Value;

        public IDocumentStore GetCommandPermissionsConnection => cmdPermissionsStore.Value;


        public BotConfiguration Configuration => configuration;

        public RavenDatabaseService()
        {
            RavenDBLocation = System.Environment.GetEnvironmentVariable("RavenIP");
            if (string.IsNullOrWhiteSpace(RavenDBLocation))
            {
                throw new InvalidOperationException("The RavenDB address, provided by the 'RavenIP' environment variable, cannot be blank!");
            }
            coreDocumentStore = new Lazy<IDocumentStore>(() => CreateDocumentStore("erector_core").Initialize());
            userDocumentStore = new Lazy<IDocumentStore>(() => CreateDocumentStore("erector_users").Initialize());
            markovDocumentStore = new Lazy<IDocumentStore>(() => CreateDocumentStore("erector_markov").Initialize());
            cmdPermissionsStore = new Lazy<IDocumentStore>(() => CreateDocumentStore("erector_command_permissions").Initialize());
        }

        private DocumentStore CreateDocumentStore(string databaseLocation)
        {
            return new DocumentStore
            {
                Urls = new[] { RavenDBLocation },
                Database = databaseLocation
            };
        }

        public async Task InitializeService()
        {
            GetBotConfiguration();            
            await Task.Yield();
        }


        private BotConfiguration GetBotConfiguration()
        {
            if (ReferenceEquals(configuration, null))
            {
                using (var session = coreDocumentStore.Value.OpenSession())
                {
                    configuration = session.Load<BotConfiguration>("configuration");
                }
            }
            return configuration;
        }

    }
}