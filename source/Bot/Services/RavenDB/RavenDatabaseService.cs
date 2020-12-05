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
            GetOrAddDocumentStore("erector_dungeoneering");
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }



        public async Task InitializeService()
        {
            await GetBotConfigurationAsync(reload: false);            
            await Task.Yield();
        }

        public IDocumentStore GetOrAddDocumentStore(string database)
        {
            return stores.GetOrAdd(database, (name) => new Lazy<IDocumentStore>(CreateDocumentStore(name).Initialize())).Value;
        }

        public async Task ReloadConfigurationAsync()
        {
            await GetBotConfigurationAsync(reload: true);
        }


        private DocumentStore CreateDocumentStore(string databaseLocation)
        {
            return new DocumentStore
            {
                Urls = new[] { RavenDBLocation },
                Database = databaseLocation
            };
        }

        private async Task<BotConfiguration> GetBotConfigurationAsync(bool reload = false)
        {
            if (ReferenceEquals(configuration, null) || reload)
            {
                using (var session = GetOrAddDocumentStore("erector_core").OpenAsyncSession())
                {
                    configuration = await session.LoadAsync<BotConfiguration>("configuration");
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