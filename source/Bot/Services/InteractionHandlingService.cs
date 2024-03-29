using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services
{

    /// <summary>
    /// All knowing all seeing interaction handler
    /// </summary>
    public sealed class InteractionHandlingService : IEileenService
    {
        private readonly InteractionService interactionService;
        private readonly DiscordSocketClient client;
        private readonly ILogger<InteractionHandlingService> logger;
        private readonly CancellationTokenSource cts;
        private readonly IServiceProvider provider;

        private readonly ConcurrentDictionary<string, InteractionModalCallbackProvider> modalCallbacks;
        private readonly ConcurrentDictionary<string, InteractionButtonCallbackProvider> buttonCallbacks;
        private readonly ConcurrentDictionary<string, InteractionSelectionCallbackProvider> selectionCallbacks;


        public bool AutoInitialize() => false;


        public InteractionHandlingService(
            InteractionService interactionService,
            DiscordSocketClient client,
            ILogger<InteractionHandlingService> logger,
            CancellationTokenSource cts,
            IServiceProvider provider)
        {
            this.interactionService = interactionService;
            this.client = client;
            this.logger = logger;
            this.cts = cts;
            this.provider = provider;
            modalCallbacks = new ConcurrentDictionary<string, InteractionModalCallbackProvider>();
            buttonCallbacks = new ConcurrentDictionary<string, InteractionButtonCallbackProvider>();
            selectionCallbacks = new ConcurrentDictionary<string, InteractionSelectionCallbackProvider>();
        }

        public async Task InitializeService()
        {
            client.Ready += async () =>
            {
                try
                {
                    logger.LogInformation("Client is READY for interactions... registering...");
                    interactionService.AddTypeConverter<string[]>(new TypeConverters.StringArrayTypeConverter());
                    await interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), provider);

                    foreach (var guild in client.Guilds)
                    {
                        await interactionService.RegisterCommandsToGuildAsync(guild.Id);
                    }
                    logger.LogInformation("Registered GUILD commands, setting up event hooks...");


                    client.InteractionCreated += async interaction =>
                    {
                        var ctx = new SocketInteractionContext(client, interaction);
                        await interactionService.ExecuteCommandAsync(ctx, provider);
                    };
                    client.ModalSubmitted += async modalSubmitted =>
                    {
                        var modalKey = modalSubmitted.Data.CustomId;

                        if (modalCallbacks.TryGetValue(modalKey, out var callbackProvider))
                        {
                            if (callbackProvider.SingleUse)
                            {
                                modalCallbacks.TryRemove(modalKey, out var _);
                            }
                            await callbackProvider.Callback(modalSubmitted);
                        }
                    };
                    client.ButtonExecuted += async buttonClicked =>
                    {
                        var buttonLookupKey = buttonClicked.Data.CustomId;
                        if (buttonCallbacks.TryGetValue(buttonLookupKey, out var callbackProvider))
                        {
                            if (callbackProvider.SingleUse)
                            {
                                buttonCallbacks.TryRemove(buttonLookupKey, out var _);
                            }
                            await callbackProvider.Callback(buttonClicked);
                        }
                    };
                    client.SelectMenuExecuted += async menuItemSelected =>
                    {
                        var menuKey = menuItemSelected.Data.CustomId;
                        if (selectionCallbacks.TryGetValue(menuKey, out var callbackProvider))
                        {
                            if (callbackProvider.SingleUse)
                            {
                                selectionCallbacks.TryRemove(menuKey, out var _);
                            }
                            await callbackProvider.Callback(menuItemSelected);
                        }
                    };

                    interactionService.SlashCommandExecuted += async (command, context, result) =>
                    {
                        if (result.IsSuccess) return;
                        await context.Interaction.RespondAsync($"Error: {result.ErrorReason}");
                    };
                    logger.LogInformation("Hooked successfully!");
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to register interaction commands and/or hook the events (somehow????)");
                    cts.Cancel();
                }
            };
            await Task.CompletedTask;
        }



        public void RegisterCallbackHandler(string name, InteractionModalCallbackProvider provider, bool replace = false)
        {
            if (modalCallbacks.ContainsKey(name) && !replace) return;
            modalCallbacks.TryAdd(name, provider);
        }

        public void RegisterCallbackHandler(string name, InteractionButtonCallbackProvider provider, bool replace = false)
        {
            if (buttonCallbacks.ContainsKey(name) && !replace) return;
            buttonCallbacks[name] = provider;
            buttonCallbacks.TryAdd(name, provider);
        }

        public void RegisterCallbackHandler(string name, InteractionSelectionCallbackProvider provider, bool replace = false)
        {
            if (selectionCallbacks.ContainsKey(name) && !replace) return;
            selectionCallbacks.TryAdd(name, provider);
        }

        public void RemoveModalCallbacks(params string[] names)
        {
            foreach(var name in names)
            {
                modalCallbacks.TryRemove(name, out var _);
            }
        }

        public void RemoveButtonCallbacks(params string[] names)
        {
            foreach(var name in names)
            {
                buttonCallbacks.TryRemove(name, out var _);
            }
        }

        public void RemoveSelectionCallbacks(params string[] names)
        {
            foreach(var name in names)
            {
                selectionCallbacks.TryRemove(name, out var _);
            }
        }

    }


    #region callback provider records
    public record struct InteractionModalCallbackProvider(
        Func<SocketModal, Task> Callback,
        bool SingleUse = false);

    public record struct InteractionButtonCallbackProvider(
        Func<SocketMessageComponent, Task> Callback,
        bool SingleUse = false);

    public record struct InteractionSelectionCallbackProvider(
        Func<SocketMessageComponent, Task> Callback,
        bool SingleUse = false);


    #endregion

}
