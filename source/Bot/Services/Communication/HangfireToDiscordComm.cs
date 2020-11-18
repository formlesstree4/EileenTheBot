using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Bot.Services.Communication
{
    public sealed class HangfireToDiscordComm
    {

        public static HangfireToDiscordComm Instance { get; set; }

        private readonly IServiceProvider services;

        private DiscordSocketClient client;

        private MarkovService markovService;


        /// <summary>
        ///     Do NOT use this shit
        /// </summary>
        public HangfireToDiscordComm(IServiceProvider services)
        {
            this.services = services;
            this.client = services.GetRequiredService<DiscordSocketClient>();
            this.markovService = services.GetRequiredService<MarkovService>();
            Instance = this;
        }


        public async Task InitializeService()
        {
            this.ScheduleJobs();
            await Task.Yield();
        }

        public async Task SendMessageToChannel(ulong channelId, string message)
        {
            var c = client.GetChannel(channelId);
            if (c is IMessageChannel mc)
            {
                await mc.SendMessageAsync(message);
            }
        }

        public async Task SendMessageToUser(ulong userId, string message)
        {
            var c = client.GetUser(userId);
            var channel = await c.GetOrCreateDMChannelAsync();
            await channel.SendMessageAsync(message);
        }

        public async Task SaveMarkovChains() => await markovService.SaveServiceAsync();


        private void ScheduleJobs()
        {
            BackgroundJob.Schedule(() => SendMessageToUser(105497358833336320, "Hey there Jira! I am alive~"), TimeSpan.Zero);
            RecurringJob.AddOrUpdate("markovSaveContent", () => SaveMarkovChains(), Cron.Hourly);
        }

    }
}