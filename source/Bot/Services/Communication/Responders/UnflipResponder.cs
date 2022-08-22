using Bot.Services.RavenDB;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Bot.Services.Communication.Responders
{
    internal sealed class UnflipResponder : MessageResponderBase
    {
        public UnflipResponder(
            DiscordSocketClient discordSocketClient,
            RavenDatabaseService ravenDatabaseService,
            ServerConfigurationService serverConfigurationService,
            ILogger<MessageResponderBase> logger) : base(discordSocketClient, ravenDatabaseService, serverConfigurationService, logger)
        {
        }

        internal override bool CanRespondViaPM => true;

        internal override bool CanRespondInNsfw => true;

        internal override Task<bool> CanRespondToMessage(SocketUserMessage message, ulong instanceId) => Task.FromResult(true);

        internal override Task<(bool, string)> DoesContainTriggerWord(SocketUserMessage message, ulong instanceId)
        {
            var messageText = message.CleanContent;
            if (messageText.Contains("(╯°□°）╯︵ ┻━┻"))
            {
                return Task.FromResult((true, "(╯°□°）╯︵ ┻━┻"));
            }
            else
            {
                return Task.FromResult((false, ""));
            }
        }

        internal override Task<string> GenerateResponse(string triggerWord, SocketUserMessage message, ulong instanceId)
        {
            return Task.FromResult("┬─┬ ノ( ゜-゜ノ)");
        }
    }
}
