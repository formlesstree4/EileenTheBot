using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Bot.Services;
using System.Threading;
using Bot.Services.Booru;

namespace Bot
{

    class Program
    {

        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            
            using (var services = ConfigureServices())
            {
                var client = services.GetRequiredService<DiscordSocketClient>();

                client.Log += LogAsync;
                services.GetRequiredService<CommandService>().Log += LogAsync;

                // Tokens should be considered secret data and never hard-coded.
                // We can read from the environment variable to avoid hardcoding.
                await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DiscordApiToken"));
                await client.StartAsync();

                // Here we initialize the logic required to register our commands.
                await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

                await Task.Delay(Timeout.Infinite);
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()

                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<Func<LogMessage, Task>>(LogAsync)
                .AddSingleton<CredentialsService>()
                .AddSingleton<Danbooru>()
                .AddSingleton<e621>()
                .AddSingleton<Gelbooru>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<BetterPaginationService>()
                .BuildServiceProvider();
        }




    }

}
