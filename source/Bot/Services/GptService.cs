using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Bot.Services.RavenDB;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Bot.Services
{
    public sealed class GptService
    {
        
        private readonly string _triggerWord;
        private readonly DiscordSocketClient _discord;
        private readonly LinkedList<string> _archiveOfMessages;
        private readonly ulong _validServerId = 167274926883995648;
        private readonly int _backlogToKeep;
        private readonly string _endpointUrl;
        private readonly HttpClient _client = new HttpClient();
        private readonly string _replacementName = "Coolswift";

        public GptService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            var configuration = services.GetRequiredService<RavenDatabaseService>().Configuration;

            _triggerWord = configuration.MarkovTrigger ?? "erector";
            _endpointUrl = configuration.GptUrl;
            _backlogToKeep = configuration.History;
            Console.WriteLine($"Trigger Word: {_triggerWord}");
            Console.WriteLine($"Historical Context: {_backlogToKeep}");
            _archiveOfMessages = new LinkedList<string>();
        }

        public void InitializeService()
        {
            _discord.MessageReceived += HandleIncomingMessage;
        }

        // Suppress the warning about using an async method when no code is async.
        #pragma warning disable CS1998
        private async Task HandleIncomingMessage(SocketMessage rawMessage)
        {
            if (!(rawMessage is SocketUserMessage message)) return;
            if (!(message.Channel is IGuildChannel gc)) return;
            if (gc.GuildId != _validServerId) return;

            var username = message.Author.Id == _discord.CurrentUser.Id ? _replacementName : message.Author.Username;
            var escapedMessage = message.Resolve(0, TagHandling.NameNoPrefix);
            var replacedMessage = escapedMessage.Replace("erector", _replacementName, true, System.Globalization.CultureInfo.InvariantCulture);
            var formattedMessage = $"{username}: {replacedMessage}";
            Console.WriteLine(formattedMessage);
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
            Task.Factory.StartNew(async () => {
                using (message.Channel.EnterTypingState())
                {
                    var finalPayload = payload + '\n' + $"{_replacementName}: ";
                    Console.WriteLine("Requesting Response...");
                    var response = await GetGptResponse(finalPayload);
                    Console.WriteLine("... response received!");
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
            Console.WriteLine($"Outgoing: {message}");
            var clientResults = await _client.PostAsync(_endpointUrl, stringContent);
            var jsonResponse = await clientResults.Content.ReadAsStringAsync();
            Console.WriteLine($"Incoming: {jsonResponse}");
            var gptResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, anonType);
            var text = gptResponse.text.Remove(0, context.Length).Split(new[] { '\n', '\r' })[0];
            if (string.IsNullOrWhiteSpace(text)) return await GetGptResponse(context, counter += 1);
            return text;
        }

    }
}