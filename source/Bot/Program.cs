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
using Bot.Models.Raven;
using Hangfire;
using Hangfire.LiteDB;

namespace Bot
{

    class Program
    {

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            GlobalConfiguration.Configuration.UseLiteDbStorage();
            var bjs = new BackgroundJobServer();

            using (var services = ConfigureServices())
            {
                var client = services.GetRequiredService<DiscordSocketClient>();
                var cts = services.GetRequiredService<CancellationTokenSource>();
                
                client.Log += LogAsync;
                // Here we initialize the logic required to register our commands.
                Console.WriteLine("Initializing Services...");
                await services.GetRequiredService<RavenDatabaseService>().InitializeService();
                await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
                services.GetRequiredService<CommandService>().Log += LogAsync;
                services.GetRequiredService<MarkovService>().InitializeService();
                services.GetRequiredService<GptService>().InitializeService();
                var configuration = services.GetRequiredService<RavenDatabaseService>().Configuration;

                // Tokens should be considered secret data and never hard-coded.
                // We can read from the environment variable to avoid hardcoding.
                await client.LoginAsync(TokenType.Bot, configuration.DiscordToken);
                await client.StartAsync();
                await client.SetStatusAsync(UserStatus.Online);
                await client.SetGameAsync(name: "A small time booru bot", streamUrl: null, type: ActivityType.CustomStatus);

                RecurringJob.AddOrUpdate(() => Console.WriteLine("From Hangfire!"), Cron.Minutely);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private ServiceProvider ConfigureServices() => new ServiceCollection()
                .AddAutoMapper(Assembly.GetExecutingAssembly())
                // .AddHangfire(config =>
                // {
                //     config.UseMemoryStorage();
                // })
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
