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


        public IDocumentStore GetCoreConnection => coreDocumentStore.Value;

        public IDocumentStore GetUserConnection => userDocumentStore.Value;

        public IDocumentStore GetMarkovConnection => markovDocumentStore.Value;

        public BotConfiguration Configuration => configuration;

        public RavenDatabaseService()
        {

            RavenDBLocation = System.Environment.GetEnvironmentVariable("RavenIP");
            if (string.IsNullOrWhiteSpace(RavenDBLocation))
            {
                // RavenDBLocation = "http://192.168.254.180:8080";
                throw new InvalidOperationException("The RavenDB address, provided by the 'RavenIP' environment variable, cannot be blank!");
            }
            coreDocumentStore = new Lazy<IDocumentStore>(() =>
            {
                var s = new DocumentStore
                {
                    Urls = new[] { RavenDBLocation },
                    Database = "erector_core"
                };

                return s.Initialize();
            });
            userDocumentStore = new Lazy<IDocumentStore>(() => 
            {
                var s = new DocumentStore
                {
                    Urls = new[] { RavenDBLocation },
                    Database = "erector_users"
                };

                return s.Initialize();
            });
            markovDocumentStore = new Lazy<IDocumentStore>(() =>
            {
                var s = new DocumentStore
                {
                    Urls = new[] { RavenDBLocation },
                    Database = "erector_markov"
                };

                return s.Initialize();
            });
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