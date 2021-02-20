﻿using System;
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
using Bot.Services.Communication;
using Hangfire.States;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore;
using Hangfire.Dashboard;
using Hangfire.Annotations;
using Bot.Services.Dungeoneering;

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
            var services = ConfigureServices();
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

            await InitializeServices(services);
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
            await LogAsync("Saving data to RavenDB...");
            await services.GetRequiredService<UserService>().SaveServiceAsync();
            await services.GetRequiredService<MarkovService>().SaveServiceAsync();
            await services.GetRequiredService<DungeoneeringMainService>().SaveServiceAsync();
            await services.GetRequiredService<ServerConfigurationService>().SaveServiceAsync();
            await LogAsync("Tasks all completed - Going offline");
        }

        private LogSeverity ParseEnvironmentLogLevel()
        {
            switch(Environment.GetEnvironmentVariable("LogLevel")?.ToUpperInvariant())
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
            switch(Environment.GetEnvironmentVariable("LogLevel")?.ToUpperInvariant())
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

        private async Task InitializeServices(ServiceProvider services)
        {
            await services.GetRequiredService<HangfireToDiscordComm>().InitializeService();
            await services.GetRequiredService<UserService>().InitializeService();
            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
            await services.GetRequiredService<MarkovService>().InitializeService();
            await services.GetRequiredService<StupidTextService>().InitializeService();
            await services.GetRequiredService<GptService>().InitializeService();
            await services.GetRequiredService<CurrencyService>().InitializeService();
            await services.GetRequiredService<MonsterService>().InitializeService();
            await services.GetRequiredService<DungeoneeringMainService>().InitializeService();
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
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
                .AddSingleton<CurrencyService>()
                .AddSingleton<CommandPermissionsService>()
                .AddSingleton<MonsterService>()
                .AddSingleton<DungeoneeringMainService>()
                .AddTransient<Random>(provider => MersenneTwister.MTRandom.Create())
                .AddSingleton<ReactionHelperService>()
                .AddSingleton<ServerConfigurationService>()
                .BuildServiceProvider();
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
