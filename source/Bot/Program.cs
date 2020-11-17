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
using Hangfire.LiteDB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;

namespace Bot
{

    class Program
    {

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {

            var ui = WebHost.CreateDefaultBuilder()
                    .UseKestrel()
                    .ConfigureServices((hfs) => {
                        // Add Hangfire services.
                        // 
                        hfs.AddHangfire(configuration => configuration
                            .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                            .UseSimpleAssemblyNameTypeSerializer()
                            .UseRecommendedSerializerSettings()
                            .UseLiteDbStorage()
                        );

                        // Add the processing server as IHostedService
                        hfs.AddHangfireServer();
                        hfs.AddMvc(config => {
                            config.EnableEndpointRouting = false;
                        });

                        hfs.AddAutoMapper(Assembly.GetExecutingAssembly())
                            .AddSingleton<RavenDatabaseService>()
                            .AddSingleton<CancellationTokenSource>()
                            .AddSingleton<DiscordSocketClient>()
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
                            .AddSingleton<GptService>();

                    })
                    .Configure((app) => {
                        // app.UseStaticFiles();
                        app.UseHangfireDashboard();
                    })
                    .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
                    .UseUrls("http://localhost:5000/")
                    .Build();

            GlobalConfiguration.Configuration.UseLiteDbStorage();
            var bjs = new BackgroundJobServer();
            var services = ui.Services;
            var client = services.GetRequiredService<DiscordSocketClient>();
            var cts = services.GetRequiredService<CancellationTokenSource>();
            var website = ui.RunAsync(cts.Token);
            
            // Here we initialize the logic required to register our commands.
            Console.WriteLine("Initializing Services...");
            
            client.Log += LogAsync;
            services.GetRequiredService<CommandService>().Log += LogAsync;

            await services.GetRequiredService<RavenDatabaseService>().InitializeService();
            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
            await services.GetRequiredService<MarkovService>().InitializeService();
            await services.GetRequiredService<StupidTextService>().InitializeService();
            services.GetRequiredService<GptService>().InitializeService();
            var configuration = services.GetRequiredService<RavenDatabaseService>().Configuration;

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
            Console.WriteLine("Disengaging Hangfire");
            bjs.Dispose();
            
            Console.WriteLine("Dumping Markov history into the DB");
            await services.GetRequiredService<MarkovService>().SaveServiceAsync();
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private ServiceProvider ConfigureServices() => new ServiceCollection()
                .AddAutoMapper(Assembly.GetExecutingAssembly())
                .AddSingleton<RavenDatabaseService>()
                .AddSingleton<CancellationTokenSource>()
                .AddSingleton<DiscordSocketClient>()
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

    }

}
