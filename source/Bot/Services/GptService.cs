using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
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

        public GptService(IServiceProvider services)
        {
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _triggerWord = Environment.GetEnvironmentVariable("MarkovTrigger") ?? "erector";
            _endpointUrl = Environment.GetEnvironmentVariable("GptUrl");
            if (!int.TryParse(Environment.GetEnvironmentVariable("History"), out _backlogToKeep))
            {
                _backlogToKeep = 10;
            }
            Console.WriteLine($"Trigger Word: {_triggerWord}");
            Console.WriteLine($"Historical Context: {_backlogToKeep}");
            _archiveOfMessages = new LinkedList<string>();
        }

        public void InitializeService()
        {
            _discord.MessageReceived += HandleIncomingMessage;
        }


        private async Task HandleIncomingMessage(SocketMessage rawMessage)
        {
            if (!(rawMessage is SocketUserMessage message)) return;
            if (!(message.Channel is IGuildChannel gc)) return;
            if (gc.GuildId != _validServerId) return;
            if (message.Author.Id == _discord.CurrentUser.Id) return;

            var username = message.Author.Id == _discord.CurrentUser.Id ? "Coolswift" : message.Author.Username;
            var escapedMessage = message.Resolve(0, TagHandling.NameNoPrefix);
            var formattedMessage = $"{username}: {escapedMessage}";
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

            // time to get the response
            Task.Factory.StartNew(async () => {
                using (message.Channel.EnterTypingState())
                {
                    var finalPayload = payload + '\n' + "Coolswift: ";
                    Console.WriteLine("Requesting Response...");
                    var response = await GetGptResponse(finalPayload);
                    Console.WriteLine("... response received!");
                    await message.Channel.SendMessageAsync(response);
                }
            });
            

        }


        private async Task<string> GetGptResponse(string context)
        {
            var message = JsonConvert.SerializeObject(new { prefix = context, length = 50 });
            var anonType = new { text = "" };
            var stringContent = new StringContent(message);
            Console.WriteLine($"Outgoing: {message}");
            var clientResults = await _client.PostAsync(_endpointUrl, stringContent);
            var jsonResponse = await clientResults.Content.ReadAsStringAsync();
            Console.WriteLine($"Incoming: {jsonResponse}");
            var gptResponse = JsonConvert.DeserializeAnonymousType(jsonResponse, anonType);
            return gptResponse.text.Remove(0, context.Length).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0];
        }

    }
}