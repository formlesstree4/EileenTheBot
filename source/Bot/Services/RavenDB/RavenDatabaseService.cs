using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models.Raven;
using Discord;
using Hangfire;
using Raven.Client.Documents;

namespace Bot.Services.RavenDB
{
    public sealed class RavenDatabaseService
    {

        private BotConfiguration configuration;
        private readonly string RavenDBLocation;
        private readonly ConcurrentDictionary<string, Lazy<IDocumentStore>> stores;
        private readonly Func<LogMessage, Task> logger;

        [Obsolete("Use GetOrAddDocumentStore()")]
        public IDocumentStore GetCoreConnection => GetOrAddDocumentStore("erector_core");

        [Obsolete("Use GetOrAddDocumentStore()")]
        public IDocumentStore GetUserConnection => GetOrAddDocumentStore("erector_users");

        [Obsolete("Use GetOrAddDocumentStore()")]
        public IDocumentStore GetMarkovConnection => GetOrAddDocumentStore("erector_markov");

        [Obsolete("Use GetOrAddDocumentStore()")]
        public IDocumentStore GetCommandPermissionsConnection => GetOrAddDocumentStore("erector_command_permissions");


        public BotConfiguration Configuration => configuration;

        public RavenDatabaseService(Func<LogMessage, Task> logger)
        {
            RavenDBLocation = System.Environment.GetEnvironmentVariable("RavenIP");
            if (string.IsNullOrWhiteSpace(RavenDBLocation))
            {
                throw new InvalidOperationException("The RavenDB address, provided by the 'RavenIP' environment variable, cannot be blank!");
            }
            stores = new ConcurrentDictionary<string, Lazy<IDocumentStore>>(StringComparer.OrdinalIgnoreCase);

            GetOrAddDocumentStore("erector_core");
            GetOrAddDocumentStore("erector_users");
            GetOrAddDocumentStore("erector_markov");
            GetOrAddDocumentStore("erector_command_permissions");
            GetOrAddDocumentStore("erector_dungeoneering");
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }



        public async Task InitializeService()
        {
            GetBotConfiguration();            
            await Task.Yield();
        }

        public IDocumentStore GetOrAddDocumentStore(string database)
        {
            return stores.GetOrAdd(database, (name) => new Lazy<IDocumentStore>(CreateDocumentStore(name).Initialize())).Value;
        }



        private DocumentStore CreateDocumentStore(string databaseLocation)
        {
            return new DocumentStore
            {
                Urls = new[] { RavenDBLocation },
                Database = databaseLocation
            };
        }

        private BotConfiguration GetBotConfiguration()
        {
            if (ReferenceEquals(configuration, null))
            {
                using (var session = GetOrAddDocumentStore("erector_core").OpenSession())
                {
                    configuration = session.Load<BotConfiguration>("configuration");
                }
            }
            return configuration;
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(RavenDatabaseService), message));
        }

    }
}