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
        private readonly Func<LogMessage, Task> WriteLog;
        private DiscordSocketClient client;


        public HangfireToDiscordComm(IServiceProvider services, Func<LogMessage, Task> logger)
        {
            this.services = services;
            this.WriteLog = logger;
            this.client = services.GetRequiredService<DiscordSocketClient>();
        }


        public async Task InitializeService()
        {
            Write("Creating initial jobs...");
            this.ScheduleJobs();
            await Task.Yield();
        }

        public async Task SendMessageToChannel(ulong channelId, string message)
        {
            Write($"Fetching Channel (ID {channelId})", LogSeverity.Verbose);
            var c = client.GetChannel(channelId);
            if (c is IMessageChannel mc)
            {
                Write($"A message is being sent to {mc.Name}");
                await mc.SendMessageAsync(message);
            }
        }

        public async Task SendMessageToUser(ulong userId, string message)
        {
            var dc = client as IDiscordClient;
            Write($"Fetching User (ID {userId})", LogSeverity.Verbose);
            var user = await dc.GetUserAsync(userId);
            if (user is null)
            {
                Write($"Unable to retrieve User (ID {userId})", LogSeverity.Error);
                return;
            }
            var channel = await user.GetOrCreateDMChannelAsync();
            if (channel is null)
            {
                Write($"Failed to create a DM channel for {user.Username}", LogSeverity.Error);
                return;
            }
            Write($"Sending private message to {user.Username}!");
            await channel.SendMessageAsync(message);
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            WriteLog(new LogMessage(severity, nameof(HangfireToDiscordComm), message));
        }

        private void ScheduleJobs()
        {
            BackgroundJob.Schedule(() => SendMessageToUser(105497358833336320, "Hey man. I'm alive and well"), TimeSpan.FromSeconds(5));
        }

    }

}