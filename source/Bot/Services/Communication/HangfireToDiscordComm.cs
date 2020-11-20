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

        private readonly IServiceProvider services;

        private DiscordSocketClient client;


        public HangfireToDiscordComm(IServiceProvider services)
        {
            this.services = services;
            this.client = services.GetRequiredService<DiscordSocketClient>();
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
            var c = client.GetUser("formlesstree4", "2035");
            if (c is null) {
                Console.WriteLine($"ERROR GETTING USER DETAILS ({userId})");
                return;
            }
            var channel = await c.GetOrCreateDMChannelAsync();
            await channel.SendMessageAsync(message);
        }


        private void ScheduleJobs()
        {
            //BackgroundJob.Schedule(() => SendMessageToUser(105497358833336320, "Hey man. I'm alive and well"), TimeSpan.FromSeconds(5));
        }

    }

}