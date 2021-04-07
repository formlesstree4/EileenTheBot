using AutoMapper;
using Bot.Models;
using Bot.Services;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hangfire;
using Hangfire.Annotations;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Hangfire.States;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Bot
{

    class Program
    {

        private LogSeverity currentLogLevel = LogSeverity.Info;


        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            await LogAsync("Entering MainAsync - Good Day", LogSeverity.Verbose);
            await LogAsync("Initializing Eileen... please wait!");
            currentLogLevel = ParseEnvironmentLogLevel();
            await LogAsync($"Current Log Level: {currentLogLevel}", LogSeverity.Verbose);
            await LogAsync("Invoking ConfigureServices()...", LogSeverity.Verbose);
            var serviceConfiguration = await ConfigureServices();
            var services = serviceConfiguration.Item1;

            await LogAsync("ConfigureServices() has completed.", LogSeverity.Verbose);
            var client = services.GetRequiredService<DiscordSocketClient>();
            var cts = services.GetRequiredService<CancellationTokenSource>();
            await LogAsync($"Attaching DiscordSocketClient logger to {nameof(LogAsync)}", LogSeverity.Verbose);
            client.Log += LogAsync;
            await LogAsync($"Attaching the CommandService logger to {nameof(LogAsync)}", LogSeverity.Verbose);
            services.GetRequiredService<CommandService>().Log += LogAsync;
            await LogAsync("Most Discord related functionality has been setup - moving on...");
            await LogAsync($"Initializing RavenDB connectivity...");
            var ravenService = services.GetRequiredService<RavenDatabaseService>();
            await ravenService.InitializeService();

            await LogAsync("Beginning Hangfire initialization");
            var configuration = ravenService.Configuration;
            var hangfireActivator = new SpecialActivator(services);
            var connString = $"User ID={configuration.RelationalDatabase.Username};Password={configuration.RelationalDatabase.Password};Host={configuration.RelationalDatabase.Hostname};Port=5432;Database={configuration.RelationalDatabase.Database};Pooling=true;";
            GlobalConfiguration.Configuration.UsePostgreSqlStorage(connString, new PostgreSqlStorageOptions()
            {
                UseNativeDatabaseTransactions = true,
                QueuePollInterval = TimeSpan.FromSeconds(5),
                InvisibilityTimeout = TimeSpan.FromSeconds(5)
            });
            GlobalConfiguration.Configuration.UseColouredConsoleLogProvider(ParseEnvironmentLogLevelForHangfire());
            GlobalConfiguration.Configuration.UseActivator(hangfireActivator);
            await LogAsync("Hangfire configuration has been established, setting up Job Server...");

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
                    ServerName = "Eileen-Host",
                }, JobStorage.Current);

            await LogAsync("Job Server has been setup and configured.");
            await LogAsync("Initializing the remaining Eileen services...");

            await InitializeServices(services, serviceConfiguration.Item2);
            await LogAsync("All services initialized, logging into Discord");
            await client.LoginAsync(TokenType.Bot, configuration.DiscordToken);
            await client.StartAsync();
            await client.SetStatusAsync(UserStatus.Online);
            await client.SetGameAsync(name: "A bot made by formlesstree4", streamUrl: null, type: ActivityType.CustomStatus);
            await LogAsync("Connected to Discord without error!");
            await LogAsync("Creating WebUI for Hangfire's Dashboard");
            var ui = WebHost
                .CreateDefaultBuilder()
                .UseSetting("connection-string", connString)
                .UseKestrel()
                .UseUrls("http://*:5000")
                .UseStartup<Startup>()
                .Build();
#pragma warning disable CS4014
            ui.RunAsync();
#pragma warning restore CS4014
            await LogAsync("The Hangfire Dashboard has been initialized on port 5000 listening to all addresses");
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException)
            {
                await LogAsync("The cancellation token has been canceled. Eileen is now shutting down");
            }
            await LogAsync("Shutting down Hangfire...");
            bjs.Dispose();
            await SaveServices(services, serviceConfiguration.Item2);
            await LogAsync("Tasks all completed - Going offline");
        }

        private LogSeverity ParseEnvironmentLogLevel()
        {
            switch (Environment.GetEnvironmentVariable("LogLevel")?.ToUpperInvariant())
            {
                case "CRITICAL":
                case "0":
                    return LogSeverity.Critical;
                case "ERROR":
                case "1":
                    return LogSeverity.Error;
                case "WARNING":
                case "2":
                    return LogSeverity.Warning;
                case "INFO":
                case "3":
                case null:
                    return LogSeverity.Info;
                case "VERBOSE":
                case "4":
                    return LogSeverity.Verbose;
                case "DEBUG":
                case "5":
                    return LogSeverity.Debug;
            }
            throw new InvalidOperationException("Somehow failed to parse the logging level");
        }

        private Hangfire.Logging.LogLevel ParseEnvironmentLogLevelForHangfire()
        {
            switch (Environment.GetEnvironmentVariable("LogLevel")?.ToUpperInvariant())
            {
                case "CRITICAL":
                case "0":
                    return Hangfire.Logging.LogLevel.Fatal;
                case "ERROR":
                case "1":
                    return Hangfire.Logging.LogLevel.Error;
                case "WARNING":
                case "2":
                    return Hangfire.Logging.LogLevel.Warn;
                case "INFO":
                case "3":
                case null:
                    return Hangfire.Logging.LogLevel.Info;
                case "VERBOSE":
                case "4":
                    return Hangfire.Logging.LogLevel.Trace;
                case "DEBUG":
                case "5":
                    return Hangfire.Logging.LogLevel.Debug;
            }
            throw new InvalidOperationException("Somehow failed to parse the logging level");
        }

        private async Task InitializeServices(ServiceProvider services, IEnumerable<Type> serviceTypes)
        {
            // Manually start up UserService first. Then handle everything else.
            await services.GetRequiredService<UserService>().InitializeService();
            foreach (var type in serviceTypes)
            {
                var service = services.GetRequiredService(type);
                if (service is null)
                {
                    await LogAsync($"Unable to locate {type.Name}", LogSeverity.Warning);
                    continue;
                }
                await LogAsync($"Initializing {type.Name}...");
                await (service as IEileenService).InitializeService();
            }
            await services.GetRequiredService<CommandHandlingService>().InitializeService();
        }

        private async Task SaveServices(ServiceProvider services, IEnumerable<Type> serviceTypes)
        {
            foreach (var type in serviceTypes)
            {
                var service = services.GetRequiredService(type);
                if (service is null)
                {
                    await LogAsync($"Unable to locate {type.Name}", LogSeverity.Warning);
                    continue;
                }
                await LogAsync($"Saving {type.Name}...");
                await (service as IEileenService).SaveServiceAsync();
            }
        }

        private async Task<Tuple<ServiceProvider, IEnumerable<Type>>> ConfigureServices()
        {
            // Let's discover all the types that implement
            // IEileenService and ensure we register them
            // as Singletons inside the ServiceCollection.
            var eileenServices = (from assemblies in AppDomain.CurrentDomain.GetAssemblies()
                                  let types = assemblies.GetTypes()
                                  let services = (from t in types
                                                  where t.IsAssignableTo(typeof(IEileenService)) &&
                                                  !t.IsAbstract && !t.IsInterface
                                                  select t)
                                  select services).SelectMany(c => c).ToList();

            var svc = new ServiceCollection()
                // Manually add services that do NOT implement IEileenService
                .AddAutoMapper(Assembly.GetExecutingAssembly())
                .AddTransient<Random>(provider => MersenneTwister.MTRandom.Create())
                .AddSingleton<CancellationTokenSource>()
                .AddSingleton<Func<LogMessage, Task>>(LogAsync)
                .AddSingleton<DiscordSocketClient>((services) =>
                {
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
                .AddSingleton<CommandService>()
                .AddSingleton<ServiceManager>();
            await LogAsync($"Discovered {eileenServices.Count} service(s)");
            foreach (var s in eileenServices)
            {
                var attr = s.GetCustomAttribute<ServiceTypeAttribute>();
                var serviceType = attr?.ServiceType ?? ServiceType.Singleton;
                await LogAsync($"Registering {s.Name} as {serviceType}");
                switch (serviceType)
                {
                    case ServiceType.Scoped:
                        svc.AddScoped(s);
                        break;
                    case ServiceType.Transient:
                        svc.AddTransient(s);
                        break;
                    case ServiceType.Singleton:
                        svc.AddSingleton(s);
                        break;
                }
            }

            return new Tuple<ServiceProvider, IEnumerable<Type>>(svc.BuildServiceProvider(), eileenServices);
        }



        private Task LogAsync(string message, LogSeverity severity = LogSeverity.Info)
            => LogAsync(new LogMessage(severity, "Eileen", message));

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

    public sealed class Startup
    {

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHangfire(config =>
                config.UsePostgreSqlStorage(Configuration.GetValue<string>("connection-string")));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }
            app.UseHangfireDashboard(options: new DashboardOptions()
            {
                DashboardTitle = "Erector's Background Erections",
                Authorization = new[] { new HangfireAutoAuthenticationFilter() },
                AppPath = null
            }, storage: JobStorage.Current);
        }
    }

    sealed class SpecialActivator : JobActivator
    {

        private readonly IServiceProvider provider;

        public SpecialActivator(IServiceProvider provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }


        public override object ActivateJob(Type jobType)
        {
            return provider.GetRequiredService(jobType);
        }
    }

    sealed class HangfireAutoAuthenticationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext context) => true;
    }

}
