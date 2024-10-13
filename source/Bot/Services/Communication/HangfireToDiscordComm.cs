using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Bot.Services.Communication
{
    [Summary("Communication service that exposes Discord to the Hangfire background services")]
    public sealed class HangfireToDiscordComm : IEileenService
    {

        private readonly DiscordSocketClient _client;
        private readonly ILogger<HangfireToDiscordComm> _logger;

        public HangfireToDiscordComm(
            DiscordSocketClient client,
            ILogger<HangfireToDiscordComm> logger)
        {
            _client = client;
            _logger = logger;
        }


        public async Task InitializeService()
        {
            _logger.LogTrace("Creating initial jobs...");
            ScheduleJobs();
            await Task.Yield();
        }

        public async Task SendMessageToChannel(ulong channelId, string message)
        {
            _logger.LogTrace("Fetching Channel (ID {channelId})", channelId);
            var c = _client.GetChannel(channelId);
            if (c is IMessageChannel mc)
            {
                _logger.LogTrace("A message is being sent to {name}", mc.Name);
                await mc.SendMessageAsync(message);
            }
        }

        public async Task SendMessageToUser(ulong userId, string message)
        {
            IDiscordClient dc = _client;
            var user = await dc.GetUserAsync(userId);
            if (user is null)
            {
                _logger.LogWarning("Unable to retrieve User (ID {userId})", userId);
                return;
            }
            var channel = await user.CreateDMChannelAsync();
            if (channel is null)
            {
                _logger.LogWarning("Failed to create a DM channel for {username}", user.Username);
                return;
            }
            _logger.LogTrace("Sending private message to {username}!", user.Username);
            await channel.SendMessageAsync(message);
        }

        private void ScheduleJobs()
        {
            // BackgroundJob.Schedule(() => SendMessageToUser(105497358833336320, "Hey man. I'm alive and well"), TimeSpan.FromSeconds(5));
        }

    }

}
