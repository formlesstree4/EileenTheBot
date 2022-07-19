using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.Services
{
    public sealed class ModalHandlingService : IEileenService
    {
        private readonly DiscordSocketClient client;
        private readonly Dictionary<string, ModalCallbackProvider> callbacks;

        public ModalHandlingService(DiscordSocketClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            callbacks = new Dictionary<string, ModalCallbackProvider>();
        }


        public async Task InitializeService()
        {
            client.ModalSubmitted += async modal =>
            {
                var modalKey = modal.Data.CustomId;

                if (callbacks.TryGetValue(modalKey, out var callbackProvider))
                {
                    if (callbackProvider.SingleUse)
                    {
                        callbacks.Remove(modalKey);
                    }
                    await callbackProvider.Callback(modal);
                }
            };
            await Task.CompletedTask;
        }

        
        public void RegisterCallbackHandler(string name, ModalCallbackProvider provider, bool replace = false)
        {
            if (callbacks.ContainsKey(name) && !replace) return;
            callbacks[name] = provider;
        }

    }

    public record struct ModalCallbackProvider(
        Func<SocketModal, Task> Callback,
        bool SingleUse = false);
    

}
