using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Bot.Services;
using System.Threading;
using Bot.Services.Booru;
using AutoMapper;
using System.Reflection;
using Bot.Services.RavenDB;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.Server;
using Bot.Services.Communication;
using Hangfire.States;

namespace Bot
{

    class Program
    {

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var services = ConfigureServices();
            var client = services.GetRequiredService<DiscordSocketClient>();
            var cts = services.GetRequiredService<CancellationTokenSource>();

            // Here we initialize the logic required to register our commands.
            Console.WriteLine("Initializing Services...");
            
            client.Log += LogAsync;
            services.GetRequiredService<CommandService>().Log += LogAsync;
            var ravenService = services.GetRequiredService<RavenDatabaseService>();
            await ravenService.InitializeService();

            // Setup hangfire real quick...
            var configuration = ravenService.Configuration;
            var hangfireActivator = new SpecialActivator(services);

            GlobalConfiguration.Configuration.UsePostgreSqlStorage($"User ID={configuration.RelationalDatabase.Username};Password={configuration.RelationalDatabase.Password};Host={configuration.RelationalDatabase.Hostname};Port=5432;Database={configuration.RelationalDatabase.Database};Pooling=true;", new PostgreSqlStorageOptions()
            {
                UseNativeDatabaseTransactions = true,
                QueuePollInterval = TimeSpan.FromSeconds(5),
                InvisibilityTimeout = TimeSpan.FromSeconds(5)
            });
            GlobalConfiguration.Configuration.UseColouredConsoleLogProvider(Hangfire.Logging.LogLevel.Info);
            GlobalConfiguration.Configuration.UseActivator(hangfireActivator);
            
            var bjs = new BackgroundJobServer(
                new BackgroundJobServerOptions
                {
                    Activator = hangfireActivator,
                    WorkerCount = Math.Min(Environment.ProcessorCount * 5, 20),
                    Queues = new[] { EnqueuedState.DefaultQueue },
                    StopTimeout = TimeSpan.FromSeconds(10),
                    ShutdownTimeout = TimeSpan.FromSeconds(10),
                    SchedulePollingInterval = TimeSpan.FromSeconds(10),
                    HeartbeatInterval = TimeSpan.FromSeconds(30),
                    ServerTimeout = TimeSpan.FromSeconds(30),
                    ServerCheckInterval = TimeSpan.FromSeconds(30),
                    CancellationCheckInterval = TimeSpan.FromSeconds(5),
                    FilterProvider = null,
                    TaskScheduler = TaskScheduler.Default,
                    ServerName = "Eileen",
                }, JobStorage.Current);

            await services.GetRequiredService<HangfireToDiscordComm>().InitializeService();
            await services.GetRequiredService<UserService>().InitializeService();
            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
            await services.GetRequiredService<MarkovService>().InitializeService();
            await services.GetRequiredService<StupidTextService>().InitializeService();
            await services.GetRequiredService<GptService>().InitializeService();
            await client.LoginAsync(TokenType.Bot, configuration.DiscordToken);
            await client.StartAsync();
            await client.SetStatusAsync(UserStatus.Online);
            await client.SetGameAsync(name: "A bot made by formlesstree4", streamUrl: null, type: ActivityType.CustomStatus);
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch(TaskCanceledException)
            {
                Console.WriteLine("Shutdown command has been received!");
            }
            Console.WriteLine("Dumping Markov history into the DB");
            bjs.Dispose();
            await services.GetRequiredService<UserService>().SaveServiceAsync();
            await services.GetRequiredService<MarkovService>().SaveServiceAsync();
        }

        private ServiceProvider ConfigureServices() => new ServiceCollection()
            .AddAutoMapper(Assembly.GetExecutingAssembly())
            .AddSingleton<Func<LogMessage, Task>>(LogAsync)
            .AddSingleton<RavenDatabaseService>()
            .AddSingleton<CancellationTokenSource>()
            .AddSingleton<UserService>()
            .AddSingleton<DiscordSocketClient>((services) => {
                var config = new DiscordSocketConfig
                {
                    AlwaysDownloadUsers = true,
                    LogLevel = LogSeverity.Verbose,
                    DefaultRetryMode = RetryMode.RetryRatelimit,
                    UseSystemClock = true,
                    ConnectionTimeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds,
                    GatewayHost = null,
                    HandlerTimeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds,

                };
                var dsc = new DiscordSocketClient(config);
                return dsc;
            })
            .AddSingleton<HangfireToDiscordComm>()
            .AddSingleton<CredentialsService>()
            .AddSingleton<Danbooru>()
            .AddSingleton<e621>()
            .AddSingleton<Gelbooru>()
            .AddSingleton<SafeBooru>()
            .AddSingleton<Yandere>()
            .AddSingleton<CommandService>()
            .AddSingleton<CommandHandlingService>()
            .AddSingleton<BetterPaginationService>()
            .AddSingleton<StupidTextService>()
            .AddSingleton<MarkovService>()
            .AddSingleton<GptService>()
            .BuildServiceProvider();

        LogSeverity currentLogLevel = LogSeverity.Info;


        private Task LogAsync(LogMessage log)
        {
            if (log.Severity > currentLogLevel) return Task.CompletedTask;
            if (log.Exception is null)
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{log.Severity.ToString().ToUpperInvariant()}] ({log.Source}) {log.Message}");
            else
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [!{log.Severity.ToString().ToUpperInvariant()}!] ({log.Source}) {log.Exception}");
            return Task.CompletedTask;
        }

    }


    sealed class SpecialActivator : JobActivator
    {
        private readonly IServiceProvider provider;

        public SpecialActivator(IServiceProvider provider) =>
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

        public override object ActivateJob(Type jobType)
        {
            return provider.GetRequiredService(jobType);
        }
    }

}
