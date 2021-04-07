using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Bot.Services
{

    [Summary("Provides a pass-through to a web service that provides GPT-2 responses based upon the last handful of messages in the Channel for context. This only works for Guilds and will not work in private message groups with the Bot")]
    public sealed class GptService : IEileenService
    {

        private readonly string _triggerWord;
        private readonly DiscordSocketClient _discord;
        private readonly ServerConfigurationService _serverConfigurationService;
        private readonly Func<LogMessage, Task> logger;
        private readonly LinkedList<string> _archiveOfMessages;
        private readonly int _backlogToKeep;
        private readonly string _endpointUrl;
        private readonly HttpClient _client = new HttpClient();
        private readonly string _replacementName = "Coolswift";

        public GptService(
            DiscordSocketClient client,
            RavenDatabaseService ravenDatabaseService,
            ServerConfigurationService serverConfigurationService,
            Func<LogMessage, Task> logger)
        {
            _discord = client;
            this._serverConfigurationService = serverConfigurationService;
            this.logger = logger;
            var configuration = ravenDatabaseService.Configuration;

            _triggerWord = configuration.MarkovTrigger ?? "erector";
            _endpointUrl = configuration.GptUrl;
            _backlogToKeep = configuration.History;
            Write($"Trigger Word: {_triggerWord}");
            Write($"Historical Context: {_backlogToKeep}");
            _archiveOfMessages = new LinkedList<string>();
        }

        public async Task InitializeService()
        {
            _discord.MessageReceived += HandleIncomingMessage;
            await Task.Yield();
        }

        // Suppress the warning about using an async method when no code is async.
#pragma warning disable CS1998
        private async Task HandleIncomingMessage(SocketMessage rawMessage)
        {
            if (!(rawMessage is SocketUserMessage message)) return;
            if (!(message.Channel is IGuildChannel gc)) return;
            var cfg = await _serverConfigurationService.GetOrCreateConfigurationAsync(gc.GuildId);
            var position = 0;
            if (cfg.ResponderType != Models.ServerConfigurationData.AutomatedResponseType.GPT) return;
            if (message.HasCharPrefix(cfg.CommandPrefix, ref position)) return;

            var username = message.Author.Id == _discord.CurrentUser.Id ? _replacementName : message.Author.Username;
            var escapedMessage = message.Resolve(0, TagHandling.NameNoPrefix);
            var replacedMessage = escapedMessage.Replace("erector", _replacementName, true, System.Globalization.CultureInfo.InvariantCulture);
            var formattedMessage = $"{username}: {replacedMessage}";
            Write(formattedMessage);
            var payload = "";

            // Keep this lock on for the entire duration
            lock (_archiveOfMessages)
            {
                _archiveOfMessages.AddLast(formattedMessage);
                if (_archiveOfMessages.Count > _backlogToKeep)
                {
                    _archiveOfMessages.RemoveFirst();
                }
                payload = string.Join('\n', _archiveOfMessages);
            }

            // Don't let erector respond to itself. However, we do need to keep track of
            // what erector HAS said so if erector did say something, then that's OK.
            if (escapedMessage.IndexOf(_triggerWord, StringComparison.OrdinalIgnoreCase) == -1) return;
            if (message.Author.Id == _discord.CurrentUser.Id) return;

            // time to get the response
            // Disable warning that we are not using await here
            // as we do not want to wait for this to finish!
#pragma warning disable CS4014
            Task.Factory.StartNew(async () =>
            {
                using (message.Channel.EnterTypingState())
                {
                    var finalPayload = payload + '\n' + $"{_replacementName}: ";
                    Write("Requesting Response...");
                    var response = await GetGptResponse(finalPayload);
                    Write("... response received!");
                    var fullResponse = $"> {escapedMessage}" + "\n" + response;
                    await message.Channel.SendMessageAsync(fullResponse);
                }
            });
#pragma warning restore CS4014

        }
#pragma warning restore CS1998

        private async Task<string> GetGptResponse(string context, int counter = 1)
        {
            if (counter > 3) return "I tried three times and got shit back";
            var message = JsonConvert.SerializeObject(new { prefix = context, length = 50 });
            var anonType = new { text = "" };
            var stringContent = new StringContent(message);
            Write($"Outgoing: {message}");
            var clientResults = await _client.PostAsync(_endpointUrl, stringContent);
            var jsonResponse = await clientResults.Content.ReadAsStringAsync();
            Write($"Incoming: {jsonResponse}");
            var gptResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, anonType);
            var text = gptResponse.text.Remove(0, context.Length).Split(new[] { '\n', '\r' })[0];
            if (string.IsNullOrWhiteSpace(text)) return await GetGptResponse(context, counter += 1);
            return text;
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, nameof(GptService), message));
        }

    }
}