using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
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
        private readonly InteractionService _interactionService;
        private readonly DiscordSocketClient _client;
        private readonly ILogger<InteractionHandlingService> _logger;
        private readonly CancellationTokenSource _cts;
        private readonly IServiceProvider _provider;

        private readonly ConcurrentDictionary<string, InteractionModalCallbackProvider> _modalCallbacks;
        private readonly ConcurrentDictionary<string, InteractionButtonCallbackProvider> _buttonCallbacks;
        private readonly ConcurrentDictionary<string, InteractionSelectionCallbackProvider> _selectionCallbacks;


        public bool AutoInitialize() => false;


        public InteractionHandlingService(
            InteractionService interactionService,
            DiscordSocketClient client,
            ILogger<InteractionHandlingService> logger,
            CancellationTokenSource cts,
            IServiceProvider provider)
        {
            _interactionService = interactionService;
            _client = client;
            _logger = logger;
            _cts = cts;
            _provider = provider;
            _modalCallbacks = new ConcurrentDictionary<string, InteractionModalCallbackProvider>();
            _buttonCallbacks = new ConcurrentDictionary<string, InteractionButtonCallbackProvider>();
            _selectionCallbacks = new ConcurrentDictionary<string, InteractionSelectionCallbackProvider>();
        }

        public async Task InitializeService()
        {
            _client.Ready += async () =>
            {
                try
                {
                    _logger.LogInformation("Client is READY for interactions... registering...");
                    _interactionService.AddTypeConverter<string[]>(new TypeConverters.StringArrayTypeConverter());
                    await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _provider);

                    foreach (var guild in _client.Guilds)
                    {
                        await _interactionService.RegisterCommandsToGuildAsync(guild.Id);
                    }
                    _logger.LogInformation("Registered GUILD commands, setting up event hooks...");


                    _client.InteractionCreated += async interaction =>
                    {
                        var ctx = new SocketInteractionContext(_client, interaction);
                        await _interactionService.ExecuteCommandAsync(ctx, _provider);
                    };
                    _client.ModalSubmitted += async modalSubmitted =>
                    {
                        var modalKey = modalSubmitted.Data.CustomId;

                        if (_modalCallbacks.TryGetValue(modalKey, out var callbackProvider))
                        {
                            if (callbackProvider.SingleUse)
                            {
                                _modalCallbacks.TryRemove(modalKey, out var _);
                            }
                            await callbackProvider.Callback(modalSubmitted);
                        }
                    };
                    _client.ButtonExecuted += async buttonClicked =>
                    {
                        var buttonLookupKey = buttonClicked.Data.CustomId;
                        if (_buttonCallbacks.TryGetValue(buttonLookupKey, out var callbackProvider))
                        {
                            if (callbackProvider.SingleUse)
                            {
                                _buttonCallbacks.TryRemove(buttonLookupKey, out var _);
                            }
                            await callbackProvider.Callback(buttonClicked);
                        }
                    };
                    _client.SelectMenuExecuted += async menuItemSelected =>
                    {
                        var menuKey = menuItemSelected.Data.CustomId;
                        if (_selectionCallbacks.TryGetValue(menuKey, out var callbackProvider))
                        {
                            if (callbackProvider.SingleUse)
                            {
                                _selectionCallbacks.TryRemove(menuKey, out var _);
                            }
                            await callbackProvider.Callback(menuItemSelected);
                        }
                    };

                    _interactionService.SlashCommandExecuted += async (command, context, result) =>
                    {
                        if (result.IsSuccess) return;
                        await context.Interaction.RespondAsync($"Error: {result.ErrorReason}");
                    };
                    _logger.LogInformation("Hooked successfully!");
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to register interaction commands and/or hook the events (somehow????)");
                    _cts.Cancel();
                }
            };
            await Task.CompletedTask;
        }



        public void RegisterCallbackHandler(string name, InteractionModalCallbackProvider provider, bool replace = false)
        {
            if (_modalCallbacks.ContainsKey(name) && !replace) return;
            _modalCallbacks.TryAdd(name, provider);
        }

        public void RegisterCallbackHandler(string name, InteractionButtonCallbackProvider provider, bool replace = false)
        {
            if (_buttonCallbacks.ContainsKey(name) && !replace) return;
            _buttonCallbacks[name] = provider;
            _buttonCallbacks.TryAdd(name, provider);
        }

        public void RegisterCallbackHandler(string name, InteractionSelectionCallbackProvider provider, bool replace = false)
        {
            if (_selectionCallbacks.ContainsKey(name) && !replace) return;
            _selectionCallbacks.TryAdd(name, provider);
        }

        public void RemoveModalCallbacks(params string[] names)
        {
            foreach(var name in names)
            {
                _modalCallbacks.TryRemove(name, out var _);
            }
        }

        public void RemoveButtonCallbacks(params string[] names)
        {
            foreach(var name in names)
            {
                _buttonCallbacks.TryRemove(name, out var _);
            }
        }

        public void RemoveSelectionCallbacks(params string[] names)
        {
            foreach(var name in names)
            {
                _selectionCallbacks.TryRemove(name, out var _);
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
