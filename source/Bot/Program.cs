using AutoMapper;
using Bot.Models;
using Bot.Services;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Discord.Interactions;
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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Bot
{
    sealed class Program
    {
        
        static async Task Main() => await new Program().MainAsync();


        private LogSeverity currentLogLevel = LogSeverity.Info;

        private ILogger<Program> logger;

        public async Task MainAsync()
        {
            currentLogLevel = ParseEnvironmentLogLevel();
            var serviceConfiguration = ConfigureServices();
            var services = serviceConfiguration.Item1;
            logger = services.GetRequiredService<ILogger<Program>>();

            logger.LogTrace("Entering MainAsync - Good Day");
            logger.LogTrace("Initializing Eileen... please wait!");
            logger.LogTrace("Current Log Level: {currentLogLevel}", currentLogLevel);
            logger.LogInformation("Eileen - Commit Hash {hash}", Assembly.GetEntryAssembly().GetCustomAttribute<GitHashAttribute>().Hash);

            var client = services.GetRequiredService<DiscordSocketClient>();
            var cts = services.GetRequiredService<CancellationTokenSource>();
            logger.LogTrace("Attaching DiscordSocketClient logger to {method}", nameof(LogAsync));
            client.Log += LogAsync;
            logger.LogTrace("Attaching the CommandService logger to {method}", nameof(LogAsync));
            services.GetRequiredService<CommandService>().Log += LogAsync;
            logger.LogTrace("Attaching the InteractionService logger to {method}", nameof(LogAsync));
            services.GetRequiredService<InteractionService>().Log += LogAsync;
            logger.LogInformation("Most Discord related functionality has been setup - moving on...");
            logger.LogInformation("Initializing RavenDB connectivity...");
            var ravenService = services.GetRequiredService<RavenDatabaseService>();
            await ravenService.InitializeService();

            logger.LogInformation("Beginning Hangfire initialization");
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
            logger.LogInformation("Hangfire configuration has been established, setting up Job Server...");

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
                    ServerTimeout = TimeSpan.FromMinutes(30),
                    ServerCheckInterval = TimeSpan.FromMinutes(30),
                    CancellationCheckInterval = TimeSpan.FromSeconds(5),
                    FilterProvider = null,
                    TaskScheduler = TaskScheduler.Default,
                    ServerName = "Eileen-Host"
                }, JobStorage.Current);

            logger.LogInformation("Job Server has been setup and configured.");
            logger.LogInformation("Initializing the remaining Eileen services...");

            await InitializeServices(services, serviceConfiguration.Item2);
            logger.LogInformation("All services initialized, logging into Discord");
            await client.LoginAsync(TokenType.Bot, configuration.DiscordToken);
            await client.StartAsync();
            await client.SetStatusAsync(UserStatus.Online);
            await client.SetGameAsync(name: "A bot made by formlesstree4", streamUrl: null, type: ActivityType.CustomStatus);
            logger.LogInformation("Connected to Discord without error!");
            logger.LogInformation("Creating WebUI for Hangfire's Dashboard");
            var ui = WebHost
                .CreateDefaultBuilder()
                .UseSetting("connection-string", connString)
                .UseKestrel()
                .UseUrls("http://*:5000")
                .UseStartup<Startup>()
                .Build();
#pragma warning disable CS4014
            ui.RunAsync(cts.Token);
#pragma warning restore CS4014

            // hook up cancellation
            Console.CancelKeyPress += (o, c) =>
            {
                cts.Cancel();
            };

            logger.LogInformation("The Hangfire Dashboard has been initialized on port 5000 listening to all addresses");
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation("The cancellation token has been canceled. Eileen is now shutting down");
            }
            logger.LogInformation("Shutting down Hangfire...");
            bjs.Dispose();
            await SaveServices(services, serviceConfiguration.Item2);
            logger.LogInformation("Tasks all completed - Going offline");
        }

        
        private static LogSeverity ParseEnvironmentLogLevel() => Environment.GetEnvironmentVariable("LogLevel")?.ToUpper() switch
        {
            "CRITICAL" or "0" => LogSeverity.Critical,
            "ERROR" or "1" => LogSeverity.Error,
            "WARNING" or "2" => LogSeverity.Warning,
            "VERBOSE" or "4" => LogSeverity.Verbose,
            "DEBUG" or "5" => LogSeverity.Debug,
            "INFO" or "3" or null or _ => LogSeverity.Info
        };

        private static Hangfire.Logging.LogLevel ParseEnvironmentLogLevelForHangfire() => Environment.GetEnvironmentVariable("LogLevel")?.ToUpper() switch
        {
            "CRITICAL" or "0" => Hangfire.Logging.LogLevel.Fatal,
            "ERROR" or "1" => Hangfire.Logging.LogLevel.Error,
            "WARNING" or "2" => Hangfire.Logging.LogLevel.Warn,
            "VERBOSE" or "4" => Hangfire.Logging.LogLevel.Trace,
            "DEBUG" or "5" => Hangfire.Logging.LogLevel.Debug,
            "INFO" or "3" or null or _ => Hangfire.Logging.LogLevel.Info
        };

        private async Task InitializeServices(ServiceProvider services, IEnumerable<Type> serviceTypes)
        {
            // Manually start up UserService first. Then handle everything else.
            await services.GetRequiredService<UserService>().InitializeService();
            foreach (var type in serviceTypes)
            {
                var service = services.GetRequiredService(type);
                if (service is null)
                {
                    logger.LogWarning("Unable to locate {typeName}, somehow", type.Name);
                    continue;
                }
                if ((service as IEileenService).AutoInitialize())
                {
                    logger.LogInformation("Initializing {typeName}...", type.Name);
                    await (service as IEileenService).InitializeService();
                }
            }
            await services.GetRequiredService<CommandHandlingService>().InitializeService();
            await services.GetRequiredService<InteractionHandlingService>().InitializeService();
        }

        private async Task SaveServices(ServiceProvider services, IEnumerable<Type> serviceTypes)
        {
            foreach (var type in serviceTypes)
            {
                var service = services.GetRequiredService(type);
                if (service is null)
                {
                    logger.LogWarning("Unable to locate {typeName}", type.Name);
                    continue;
                }
                logger.LogInformation("Saving {typeName}...", type.Name);
                await (service as IEileenService).SaveServiceAsync();
            }
        }

        private static (ServiceProvider, IEnumerable<Type>) ConfigureServices()
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
                .AddSingleton<IConfiguration>(c =>
                {
                    return new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
                })
                .AddLogging(config =>
                {
                    config
                        .AddConfiguration(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build())
                        .AddConsole()
                        ;
                })
                .AddTransient(provider => MersenneTwister.MTRandom.Create())
                .AddSingleton<CancellationTokenSource>()
                .AddSingleton((services) =>
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
                .AddSingleton(services =>
                {
                    var client = services.GetRequiredService<DiscordSocketClient>();
                    return new InteractionService
                        (client.Rest,
                        new InteractionServiceConfig
                        {
                            UseCompiledLambda = true,
                            AutoServiceScopes = true,
                            EnableAutocompleteHandlers = true,
                            LogLevel = LogSeverity.Verbose
                        });
                })
                .AddSingleton<ServiceManager>();
            foreach (var s in eileenServices)
            {
                var attr = s.GetCustomAttribute<ServiceTypeAttribute>();
                var serviceType = attr?.ServiceType ?? ServiceType.Singleton;
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
            return (svc.BuildServiceProvider(), eileenServices);
        }



        private Task LogAsync(string message, LogSeverity severity = LogSeverity.Info)
            => LogAsync(new LogMessage(severity, "Eileen", message));

        private Task LogAsync(LogMessage log)
        {
#pragma warning disable CA2254
            switch (log.Severity)
            {
                case LogSeverity.Critical:
                    logger.LogCritical(log.Exception, log.Message);
                    break;
                case LogSeverity.Debug:
                    logger.LogDebug(log.Exception, log.Message);
                    break;
                case LogSeverity.Error:
                    logger.LogError(log.Exception, log.Message);
                    break;
                case LogSeverity.Info:
                    logger.LogInformation(log.Exception, log.Message);
                    break;
                case LogSeverity.Verbose:
                    logger.LogTrace(log.Exception, log.Message);
                    break;
                case LogSeverity.Warning:
                    logger.LogWarning(log.Exception, log.Message);
                    break;
            }
            return Task.CompletedTask;
#pragma warning restore CA2254
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
            services.AddHangfire(config => config.UseStorage(JobStorage.Current));
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
