using Bot.Models.BlackJack;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Services
{

    /// <summary>
    /// Provides management of BlackJack games for different servers
    /// </summary>
    public sealed class BlackJackService : IEileenService
    {
        private readonly DiscordSocketClient discordSocketClient;
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly InteractionHandlingService interactionHandlingService;
        private readonly UserService userService;
        private readonly CurrencyService currencyService;
        private readonly ILogger<BlackJackService> logger;
        public Dictionary<ulong, BlackJackServerDetails> blackJackDetails = new();



        public BlackJackService(
            DiscordSocketClient discordSocketClient,
            ServerConfigurationService serverConfigurationService,
            InteractionHandlingService interactionHandlingService,
            UserService userService,
            CurrencyService currencyService,
            ILogger<BlackJackService> logger)
        {
            this.discordSocketClient = discordSocketClient ?? throw new ArgumentNullException(nameof(discordSocketClient));
            this.serverConfigurationService = serverConfigurationService ?? throw new ArgumentNullException(nameof(serverConfigurationService));
            this.interactionHandlingService = interactionHandlingService ?? throw new ArgumentNullException(nameof(interactionHandlingService));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.currencyService = currencyService ?? throw new ArgumentNullException(nameof(currencyService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.discordSocketClient.Ready += HandleClientIsReady;
            this.discordSocketClient.ThreadDeleted += HandleThreadDeleted;
        }

        private Task HandleThreadDeleted(Cacheable<SocketThreadChannel, ulong> arg)
        {
            var threadId = arg.Id;
            foreach (var server in blackJackDetails)
            {
                for (int i = server.Value.ActiveGames.Count - 1; i >= 0 ; i--)
                {
                    BlackJackTable game = server.Value.ActiveGames[i];
                    if (game.ThreadId == threadId)
                    {
                        server.Value.ActiveGames.RemoveAt(i);
                        game.EndGameLoop();
                    }
                }
            }
            return Task.CompletedTask;
        }

        private async Task HandleClientIsReady()
        {
            foreach(var guild in discordSocketClient.Guilds)
            {
                var serverDetails = await serverConfigurationService.GetOrCreateConfigurationAsync(guild);
                var details = serverDetails.GetOrAddTagData(nameof(BlackJackService), () => new BlackJackServerDetails());
                details.ActiveGames = new();
                blackJackDetails.Add(guild.Id, details);
                if (details.ChannelId == null) continue;
                foreach(var thread in guild.ThreadChannels)
                {
                    if (thread.ParentChannel.Id != details.ChannelId) continue;
                    details.ActiveGames.Add(await CreateNewBlackJackGame(guild, thread.Id));
                }
            }
        }

        public async Task SaveServiceAsync()
        {
            foreach(var bjd in blackJackDetails)
            {
                var serverDetails = await serverConfigurationService.GetOrCreateConfigurationAsync(bjd.Key);
                serverDetails.SetTagData(nameof(BlackJackService), bjd.Value);
            }
        }

        public async Task<BlackJackTable> CreateNewBlackJackGame(IGuild guild, ulong? threadId = null)
        {
            if (!blackJackDetails.TryGetValue(guild.Id, out BlackJackServerDetails serverDetails))
            {
                logger.LogError("Unable to find the appropriate server details for Guild {guildId}", guild.Id);
                return null;
            };
            if (serverDetails.ChannelId == null)
            {
                logger.LogError("Guild {guildId} does not have a dedicated channel set for creating BlackJack threads", guild.Id);
                return null;
            }
            var channel = await discordSocketClient.GetChannelAsync((ulong)serverDetails.ChannelId);
            if (channel is ITextChannel textChannel)
            {
                BlackJackTable blackJackTable;
                IThreadChannel gameThread;
                var gameGuid = Guid.NewGuid();

                if (threadId == null)
                {
                    gameThread = await textChannel.CreateThreadAsync($"BlackJack Table {serverDetails.ActiveGames.Count + 1}", ThreadType.PublicThread);
                    blackJackTable = new BlackJackTable(discordSocketClient, currencyService, interactionHandlingService, gameThread, gameGuid);
                }
                else
                {
                    gameThread = await guild.GetThreadChannelAsync((ulong)threadId);
                    blackJackTable = new BlackJackTable(discordSocketClient, currencyService, interactionHandlingService, gameThread, gameGuid);
                }
                // hook up the buttons and make the inital message
                await CreateAndAttachButtonCallbacks(guild, gameThread, blackJackTable, threadId == null);
                serverDetails.ActiveGames.Add(blackJackTable);
                return blackJackTable;
            }
            else
            {
                logger.LogError("Guild {guildId} is using an inappropriate ChannelType for the BlackJack hub", guild.Id);
                return null;
            }
        }

        public void SetBlackJackChannel(IGuild guild, IChannel channel)
        {
            if (blackJackDetails.TryGetValue(guild.Id, out BlackJackServerDetails serverDetails))
            {
                serverDetails.ChannelId = channel.Id;
            }
        }

        public BlackJackTable FindBlackJackGame(IGuild guild, IThreadChannel thread)
        {
            if (blackJackDetails.TryGetValue(guild.Id, out var serverDetails))
            {
                return serverDetails.ActiveGames.FirstOrDefault(ag => ag.ThreadId == thread.Id);
            }
            return null;
        }


        private async Task CreateAndAttachButtonCallbacks(
            IGuild guild,
            IThreadChannel thread,
            BlackJackTable table,
            bool createFirstMessage)
        {
            interactionHandlingService.RegisterCallbackHandler($"join-{thread.Id}", new InteractionButtonCallbackProvider(async smc =>
            {
                var userData = await userService.GetOrCreateUserData(smc.User.Id);
                if (!table.IsPlaying(userData))
                {
                    table.AddPlayer(userData);
                    if (table.IsGameActive)
                    {
                        await smc.RespondAsync($"{smc.User.Mention} will be joining in for the next hand");
                    }
                    else
                    {
                        await smc.RespondAsync($"{smc.User.Mention} has joined the table");
                    }
                }
            }), true);
            interactionHandlingService.RegisterCallbackHandler($"leave-{thread.Id}", new InteractionButtonCallbackProvider(async smc =>
            {
                var userData = await userService.GetOrCreateUserData(smc.User.Id);
                if (table.IsPlaying(userData))
                {
                    table.RemovePlayer(userData);
                    if (table.IsGameActive)
                    {
                        await smc.RespondAsync($"{smc.User.Mention} will be leaving after the current round has completed");
                    }
                    else
                    {
                        await smc.RespondAsync($"{smc.User.Mention} has left the table");
                    }
                }
            }), true);
            if (createFirstMessage)
            {
                var builder = new ComponentBuilder()
                    .WithButton("Join", $"join-{thread.Id}")
                    .WithButton("Leave", $"leave-{thread.Id}");
                var message = await thread.SendMessageAsync($"Welcome To {thread.Name}. You may use this message to Join or Leave by pressing the appropriate button", components: builder.Build());
                await message.PinAsync();
            }
        }

    }

}
