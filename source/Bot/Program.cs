using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Bot.Services.Communication;

namespace Bot
{

    class Program
    {

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            // var ui = WebHost.CreateDefaultBuilder()
            //         .UseKestrel()
            //         .ConfigureServices((hfs) => {
            //             hfs.AddAutoMapper(Assembly.GetExecutingAssembly())
            //                 .AddSingleton<RavenDatabaseService>()
            //                 .AddSingleton<CancellationTokenSource>()
            //                 .AddSingleton<DiscordSocketClient>()
            //                 .AddSingleton<HangfireToDiscordComm>()
            //                 .AddSingleton<Func<LogMessage, Task>>(LogAsync)
            //                 .AddSingleton<CredentialsService>()
            //                 .AddSingleton<Danbooru>()
            //                 .AddSingleton<e621>()
            //                 .AddSingleton<Gelbooru>()
            //                 .AddSingleton<SafeBooru>()
            //                 .AddSingleton<Yandere>()
            //                 .AddSingleton<CommandService>()
            //                 .AddSingleton<CommandHandlingService>()
            //                 .AddSingleton<BetterPaginationService>()
            //                 .AddSingleton<StupidTextService>()
            //                 .AddSingleton<MarkovService>()
            //                 .AddSingleton<GptService>();

            //             hfs.AddHangfire(configuration => configuration
            //                 .UseSimpleAssemblyNameTypeSerializer()
            //                 .UseRecommendedSerializerSettings()
            //                 .UseLiteDbStorage()
            //             );

            //             hfs.AddHangfireServer();
            //             hfs.AddMvc(config => {
            //                 config.EnableEndpointRouting = false;
            //             });
            //         })
            //         .Configure((app) => {
            //             app.UseHangfireDashboard();
            //         })
            //         .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            //         .UseUrls("http://localhost:5000/")
            //         .Build();

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
            GlobalConfiguration.Configuration.UsePostgreSqlStorage(
                $"User ID={configuration.RelationalDatabase.Username};Password={configuration.RelationalDatabase.Password};Host={configuration.RelationalDatabase.Hostname};Port=5432;Database={configuration.RelationalDatabase.Database};Pooling=true;Min Pool Size=0;Max Pool Size=100;Connection Lifetime=0;");

            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
            await services.GetRequiredService<MarkovService>().InitializeService();
            await services.GetRequiredService<StupidTextService>().InitializeService();
            await services.GetRequiredService<HangfireToDiscordComm>().InitializeService();
            await services.GetRequiredService<GptService>().InitializeService();

            // Tokens should be considered secret data and never hard-coded.
            // We can read from the environment variable to avoid hardcoding.
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
            await services.GetRequiredService<MarkovService>().SaveServiceAsync();
        }

        private ServiceProvider ConfigureServices() => new ServiceCollection()
            .AddAutoMapper(Assembly.GetExecutingAssembly())
            .AddSingleton<RavenDatabaseService>()
            .AddSingleton<CancellationTokenSource>()
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton<HangfireToDiscordComm>()
            .AddSingleton<Func<LogMessage, Task>>(LogAsync)
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

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

    }

}
