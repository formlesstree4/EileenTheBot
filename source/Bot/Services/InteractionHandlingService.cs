using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services
{
    internal class InteractionHandlingService : IEileenService
    {
        private readonly InteractionService interactionService;
        private readonly DiscordSocketClient client;
        private readonly ILogger<InteractionHandlingService> logger;
        private readonly CancellationTokenSource cts;
        private readonly IServiceProvider provider;

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
                    await interactionService.RegisterCommandsGloballyAsync();
                    logger.LogInformation("Registered GLOBAL commands, setting up event hooks...");

                    client.InteractionCreated += async interaction =>
                    {
                        var ctx = new SocketInteractionContext(client, interaction);
                        await interactionService.ExecuteCommandAsync(ctx, provider);
                    };

                    interactionService.SlashCommandExecuted += async (command, context, result) =>
                    {
                        if (result.IsSuccess) return;
                        await context.Interaction.RespondAsync($"Error: {result.ErrorReason}");
                    };
                    logger.LogInformation("Hooked successfully...!");
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to register interaction commands");
                    cts.Cancel();
                }
            };
            await Task.CompletedTask;
        }

    }
}
