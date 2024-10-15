using Bot.Models.Eileen;
using Bot.Services;
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
using Bot.Database.Repositories;
using Npgsql;

namespace Bot
{
    sealed class Program
    {

        static async Task Main() => await new Program().MainAsync();


        private LogSeverity _currentLogLevel = LogSeverity.Info;

        private ILogger<Program> _logger = null!;

        private async Task MainAsync()
        {
            _currentLogLevel = ParseEnvironmentLogLevel();
            var serviceConfiguration = ConfigureServices();
            var services = serviceConfiguration.Item1;
            _logger = services.GetRequiredService<ILogger<Program>>();

            _logger.LogTrace("Entering MainAsync - Good Day");
            _logger.LogTrace("Initializing Eileen... please wait!");
            _logger.LogTrace("Current Log Level: {currentLogLevel}", _currentLogLevel);

            var client = services.GetRequiredService<DiscordSocketClient>();
            var cts = services.GetRequiredService<CancellationTokenSource>();
            _logger.LogTrace("Attaching DiscordSocketClient logger to {method}", nameof(LogAsync));
            client.Log += LogAsync;
            _logger.LogTrace("Attaching the CommandService logger to {method}", nameof(LogAsync));
            services.GetRequiredService<CommandService>().Log += LogAsync;
            _logger.LogTrace("Attaching the InteractionService logger to {method}", nameof(LogAsync));
            services.GetRequiredService<InteractionService>().Log += LogAsync;
            _logger.LogInformation("Most Discord related functionality has been setup - moving on...");
            _logger.LogInformation("Beginning Hangfire initialization");

            var configuration = services.GetRequiredService<IConfiguration>();
            var databaseConfiguration = configuration.GetSection("Database");
            var hangfireActivator = new SpecialActivator(services);
            var connString = $"User ID={databaseConfiguration["Username"]};Password={databaseConfiguration["Password"]};Host={databaseConfiguration["Hostname"]};Port=5432;Database=hangfire;Pooling=true;";

            GlobalConfiguration.Configuration.UsePostgreSqlStorage(a =>
            {
                a.UseNpgsqlConnection(connString);
            }, new PostgreSqlStorageOptions()
            {
                UseNativeDatabaseTransactions = true,
                QueuePollInterval = TimeSpan.FromSeconds(5),
                InvisibilityTimeout = TimeSpan.FromSeconds(5)
            });
            GlobalConfiguration.Configuration.UseColouredConsoleLogProvider(ParseEnvironmentLogLevelForHangfire());
            GlobalConfiguration.Configuration.UseActivator(hangfireActivator);
            _logger.LogInformation("Hangfire configuration has been established, setting up Job Server...");

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

            _logger.LogInformation("Job Server has been setup and configured.");
            _logger.LogInformation("Initializing the remaining Eileen services...");

            await InitializeServices(services, serviceConfiguration.Item2);
            _logger.LogInformation("All services initialized, logging into Discord");
            await client.LoginAsync(TokenType.Bot, configuration["Discord::Token"]);
            await client.StartAsync();
            await client.SetStatusAsync(UserStatus.Online);
            await client.SetGameAsync(name: "Playing Casino Games", streamUrl: null, type: ActivityType.CustomStatus);
            _logger.LogInformation("Connected to Discord without error!");
            _logger.LogInformation("Creating WebUI for Hangfire's Dashboard");
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

            _logger.LogInformation("The Hangfire Dashboard has been initialized on port 5000 listening to all addresses");
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("The cancellation token has been canceled. Eileen is now shutting down");
            }
            _logger.LogInformation("Shutting down Hangfire...");
            bjs.Dispose();
            await SaveServices(services, serviceConfiguration.Item2);
            _logger.LogInformation("Tasks all completed - Going offline");
        }


        private static LogSeverity ParseEnvironmentLogLevel() => Environment.GetEnvironmentVariable("LogLevel")?.ToUpper() switch
        {
            "CRITICAL" or "0" => LogSeverity.Critical,
            "ERROR" or "1" => LogSeverity.Error,
            "WARNING" or "2" => LogSeverity.Warning,
            "VERBOSE" or "4" => LogSeverity.Verbose,
            "DEBUG" or "5" => LogSeverity.Debug,
            _ => LogSeverity.Info
        };

        private static Hangfire.Logging.LogLevel ParseEnvironmentLogLevelForHangfire() => Environment.GetEnvironmentVariable("LogLevel")?.ToUpper() switch
        {
            "CRITICAL" or "0" => Hangfire.Logging.LogLevel.Fatal,
            "ERROR" or "1" => Hangfire.Logging.LogLevel.Error,
            "WARNING" or "2" => Hangfire.Logging.LogLevel.Warn,
            "VERBOSE" or "4" => Hangfire.Logging.LogLevel.Trace,
            "DEBUG" or "5" => Hangfire.Logging.LogLevel.Debug,
            _ => Hangfire.Logging.LogLevel.Info
        };

        private async Task InitializeServices(ServiceProvider services, IEnumerable<Type> serviceTypes)
        {
            // Manually start up UserService first. Then handle everything else.
            foreach (var type in serviceTypes)
            {
                var service = services.GetRequiredService(type);
                if (service is not IEileenService s) continue;
                if (!s.AutoInitialize()) continue;
                _logger.LogInformation("Initializing {typeName}...", type.Name);
                await s.InitializeService();
            }
            await services.GetRequiredService<InteractionHandlingService>().InitializeService();
        }

        private async Task SaveServices(ServiceProvider services, IEnumerable<Type> serviceTypes)
        {
            foreach (var type in serviceTypes)
            {
                var service = services.GetRequiredService(type);
                _logger.LogInformation("Saving {typeName}...", type.Name);
                if (service is IEileenService s)
                {
                    await s.SaveServiceAsync();
                }
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
                .AddSingleton<IConfiguration>(_ => new ConfigurationBuilder().AddJsonFile("appsettings.json").Build())
                .AddLogging(config =>
                {
                    config
                        .AddConfiguration(new ConfigurationBuilder().AddJsonFile("appsettings.json").Build())
                        .AddConsole();
                })
                .AddScoped(provider =>
                {
                    var configuration = provider.GetRequiredService<IConfiguration>();
                    var databaseConfiguration = configuration.GetSection("Database");
                    var connString = $"User ID={databaseConfiguration["Username"]};Password={databaseConfiguration["Password"]};Host={databaseConfiguration["Hostname"]};Port=5432;Database=eileen;Pooling=true;";
                    return new NpgsqlConnection(connString);
                })
                .AddTransient(_ => MersenneTwister.MTRandom.Create())
                .AddSingleton<CancellationTokenSource>()
                .AddSingleton(_ =>
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
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            // add in the repositories
            svc.AddSingleton<BlackJackRepository>();
            svc.AddSingleton<DiscordRepository>();
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
                    _logger.LogCritical(log.Exception, log.Message);
                    break;
                case LogSeverity.Debug:
                    _logger.LogDebug(log.Exception, log.Message);
                    break;
                case LogSeverity.Error:
                    _logger.LogError(log.Exception, log.Message);
                    break;
                case LogSeverity.Info:
                    _logger.LogInformation(log.Exception, log.Message);
                    break;
                case LogSeverity.Verbose:
                    _logger.LogTrace(log.Exception, log.Message);
                    break;
                case LogSeverity.Warning:
                    _logger.LogWarning(log.Exception, log.Message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
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

    internal sealed class SpecialActivator : JobActivator
    {

        private readonly IServiceProvider _provider;

        public SpecialActivator(IServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }


        public override object ActivateJob(Type jobType)
        {
            return _provider.GetRequiredService(jobType);
        }
    }

    internal sealed class HangfireAutoAuthenticationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext context) => true;
    }

}
