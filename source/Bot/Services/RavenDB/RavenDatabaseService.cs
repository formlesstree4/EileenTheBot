using Bot.Models.Raven;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Bot.Services.RavenDB
{

    [Summary("Exposes access to the RavenDB backend for the Bot to utilize")]
    public sealed class RavenDatabaseService : IEileenService
    {

        private BotConfiguration configuration;
        private readonly string RavenDBLocation;
        private readonly ConcurrentDictionary<string, Lazy<IDocumentStore>> stores;
        private readonly ILogger<RavenDatabaseService> logger;


        public BotConfiguration Configuration => configuration;

        public RavenDatabaseService(ILogger<RavenDatabaseService> logger)
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

        public bool AutoInitialize() => false;

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
            if (configuration is null || reload)
            {
                using var session = GetOrAddDocumentStore("erector_core").OpenAsyncSession();
                configuration = await session.LoadAsync<BotConfiguration>("configuration");
            }
            return configuration;
        }

    }
}
