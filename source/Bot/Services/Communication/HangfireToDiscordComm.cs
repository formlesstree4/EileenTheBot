using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Bot.Services.Communication
{
    [Summary("Communication service that exposes Discord to the Hangfire background services")]
    public sealed class HangfireToDiscordComm : IEileenService
    {

        private readonly DiscordSocketClient client;
        private readonly ILogger<HangfireToDiscordComm> logger;

        public HangfireToDiscordComm(
            DiscordSocketClient client,
            ILogger<HangfireToDiscordComm> logger)
        {
            this.client = client;
            this.logger = logger;
        }


        public async Task InitializeService()
        {
            logger.LogTrace("Creating initial jobs...");
            this.ScheduleJobs();
            await Task.Yield();
        }

        public async Task SendMessageToChannel(ulong channelId, string message)
        {
            logger.LogTrace("Fetching Channel (ID {channelId})", channelId);
            var c = client.GetChannel(channelId);
            if (c is IMessageChannel mc)
            {
                logger.LogTrace("A message is being sent to {name}", mc.Name);
                await mc.SendMessageAsync(message);
            }
        }

        public async Task SendMessageToUser(ulong userId, string message)
        {
            var dc = client as IDiscordClient;
            var user = await dc.GetUserAsync(userId);
            if (user is null)
            {
                logger.LogWarning("Unable to retrieve User (ID {userId})", userId);
                return;
            }
            var channel = await user.CreateDMChannelAsync();
            if (channel is null)
            {
                logger.LogWarning("Failed to create a DM channel for {username}", user.Username);
                return;
            }
            logger.LogTrace("Sending private message to {username}!", user.Username);
            await channel.SendMessageAsync(message);
        }

        private void ScheduleJobs()
        {
            // BackgroundJob.Schedule(() => SendMessageToUser(105497358833336320, "Hey man. I'm alive and well"), TimeSpan.FromSeconds(5));
        }

    }

}
