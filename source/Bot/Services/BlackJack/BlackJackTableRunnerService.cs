using Bot.Models.BlackJack;
using Bot.Models.BlackJack.Extensions;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services.BlackJack
{

    /// <summary>
    ///     Provides table and job management to handle running numerous tables
    /// </summary>
    public sealed class BlackJackTableRunnerService : IEileenService
    {

        private readonly ConcurrentDictionary<ulong, BlackJackTableDetails> tables = new();
        private readonly CurrencyService currencyService;
        private readonly DiscordSocketClient client;
        private readonly InteractionHandlingService interactionHandlingService;
        private readonly ILogger<BlackJackTableRunnerService> logger;
        private readonly UserService userService;

        public BlackJackTableRunnerService(
            CancellationTokenSource cancellationTokenSource,
            CurrencyService currencyService,
            DiscordSocketClient client,
            InteractionHandlingService interactionHandlingService,
            ILogger<BlackJackTableRunnerService> logger,
            UserService userService)
        {
            this.currencyService = currencyService ?? throw new ArgumentNullException(nameof(currencyService));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.interactionHandlingService = interactionHandlingService ?? throw new ArgumentNullException(nameof(interactionHandlingService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            cancellationTokenSource.Token.Register(() =>
            {
                foreach (var table in tables)
                {
                    table.Value.CancellationTokenSource.Cancel();
                }
            });
            this.client.ThreadMemberLeft += smc =>
            {
                logger.LogTrace("A user has left a thread. Lookign to see if it is a thread we are concerned with...");
                var thread = smc.Thread;
                if (tables.TryGetValue(thread.Id, out var details))
                {
                    logger.LogInformation("A user {username} has left the thread {thread}; removing them from the table (if they were even involved)", smc.Username, thread.Name);
                    RemovePlayerSafelyFromTable(details.Table, smc);
                }
                return Task.CompletedTask;
            };
        }




        /// <summary>
        /// Gets or creates a new <see cref="BlackJackTable"/> for the given <see cref="IThreadChannel"/>
        /// </summary>
        /// <param name="threadChannel">The thread channel to get or create a <see cref="BlackJackTable"/> for</param>
        /// <returns><see cref="BlackJackTable"/></returns>
        public BlackJackTable GetOrCreateBlackJackTable(IThreadChannel threadChannel)
        {
            if (!tables.TryGetValue(threadChannel.Id, out var t))
            {
                logger.LogInformation("Creating a new BlackJack table for thread {threadId}", threadChannel.Id);
                var deck = Deck.CreateDeck(4);
                var bjt = new BlackJackTable(deck);
                tables.TryAdd(threadChannel.Id, new(bjt, threadChannel));
                return bjt;
            }
            logger.LogInformation("Returning an already created table for thread {threadId}", threadChannel.Id);
            return t.Table;
        }

        /// <summary>
        ///     Starts the thread 
        /// </summary>
        /// <param name="threadChannel"></param>
        public bool StartBlackJackTableForChannel(IThreadChannel threadChannel)
        {
            logger.LogInformation("Attempting to start the game loop for thread {threadId}", threadChannel.Id);
            if (!tables.TryGetValue(threadChannel.Id, out var details)) return false;
            if (details.IsThreadCurrentlyRunning) return true;
            ThreadPool.QueueUserWorkItem(async bjtd => { await TableRunnerLoop(bjtd); }, details, false);
            logger.LogInformation("The game loop for thread {threadId} has begun", threadChannel.Id);
            return true;
        }

        /// <summary>
        /// Stops a running table and removes it from the internal dictionary
        /// </summary>
        /// <param name="threadId">The ID of the thread to look for</param>
        public void StopAndRemoveBlackJackTable(ulong threadId)
        {
            if (tables.TryGetValue(threadId, out var t))
            {
                logger.LogInformation("Attempting to stop the game loop for thread {threadId}", threadId);
                t.CancellationTokenSource.Cancel();
                tables.Remove(threadId, out _);
                logger.LogInformation("A cancellation request for {threadId} has been initiated; logs will indicate if this was successful", threadId);
            }
        }


        private async Task TableRunnerLoop(BlackJackTableDetails blackJackTableDetails)
        {
            // I'm lazy
            var thread = blackJackTableDetails.ThreadChannel;
            var table = blackJackTableDetails.Table;
            var token = blackJackTableDetails.CancellationTokenSource.Token;
            var isFirstRun = true;

            // Hook up table level Interaction Buttons
            HandleTableLevelInteractions(table, thread);
            await CreateInitialMessagesAndPinThem(blackJackTableDetails);
            blackJackTableDetails.IsThreadCurrentlyRunning = true;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    SpinWait.SpinUntil(() => IsTableReadyToPlay(table) || HasPendingPlayers(table));
                    HandlePreGameStartup(table);
                    token.ThrowIfCancellationRequested();
                    if (isFirstRun)
                    {
                        isFirstRun = false;
                        await HandleGameStartup(thread, table);
                        await MoveAndNotifyZeroBetAndBrokePlayers(thread, table);
                    }
                    else
                    {
                        await thread.SendMessageAsync("A new round of BlackJack is beginning. If you would like to Join for the next round or Leave after this round, you may do so here.", components: GetJoinAndLeaveComponents(thread.Id).Build());
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                    if (!IsTableReadyToPlay(table))
                    {
                        await thread.SendMessageAsync("It seems that there are no qualified Players available for the current round. The table will adjourn for thirty seconds and try again.");
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        continue;
                    }
                    table.IsGameActive = true;
                    token.ThrowIfCancellationRequested();
                    DealCardsToPlayers(table);
                    await thread.SendMessageAsync("The round of BlackJack has begun! All players have been dealt their hands. At any time you may request to see your hand with the button on this message OR when it is your turn", components: GetHandViewComponent(thread.Id).Build());
                    await ShowHandToChannel(thread, table.Dealer, component: null, hideFirstCard: true);
                    table.SetupTable();
                    while (table.GetNextPlayer(out var currentPlayer))
                    {
                        token.ThrowIfCancellationRequested();
                        logger.LogInformation("Player {player} is now taking their turn...", currentPlayer.Name);
                        await HandlePlayerHand(thread, table, currentPlayer);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }

                    token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    if (table.Players.Any(p => !p.Hand.IsBust))
                    {
                        await HandleDealerHand(thread, table, table.Dealer);
                    }
                    else
                    {
                        await ShowHandToChannel(thread, table.Dealer, component: null, hideFirstCard: false);
                    }
                    await HandleScoreCalculation(thread, table);
                    HandlePostGameCleanUp(table);
                    table.IsGameActive = false;
                    await thread.SendMessageAsync("The round of BlackJack has concluded!");
                }
            }
            catch (OperationCanceledException oce)
            {
                logger.LogWarning(oce, "A BlackJack game has been cancelled!");
                try
                {
                    await thread.SendMessageAsync("Attention Players - The BlackJack Runner Service has been ordered to cancel this game. Please contact the Administrator.");
                }
                catch (Exception uh)
                {
                    logger.LogCritical(uh, "Unable to notify thread of game cancellation. It is possible the thread no longer exists on Discord!");
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "A critical error has caused the BlackJack game loop to exit! Examine the stacktrace for more details");
                try
                {
                    await thread.SendMessageAsync("Attention Players - The BlackJack Runner Service has encountered an unrecoverable error and is immediately shutting down the game table. Please contact the Administrator.");
                }
                catch (Exception uh)
                {
                    logger.LogCritical(uh, "Unable to notify thread of game cancellation. It is possible the thread no longer exists on Discord!");
                }
            }
            blackJackTableDetails.IsThreadCurrentlyRunning = false;
            RemoveTableLevelInteractions(thread.Id);
        }

        /// <summary>
        ///     Safely adds a <see cref="IUser"/> to the supplied <see cref="BlackJackTable"/>
        /// </summary>
        /// <param name="table">The table to add the user to</param>
        /// <param name="user">A reference to the Discord <see cref="IUser"/></param>
        /// <returns>A promise that, if true, means the player was added successfully</returns>
        public async Task<bool> AddPlayerSafelyToTable(BlackJackTable table, IUser user)
        {
            if (!table.PendingPlayers.Any(pp => pp.User.UserId == user.Id) &&
                !table.Players.Any(p => p.User.UserId == user.Id))
            {
                var userData = await userService.GetOrCreateUserData(user);
                var blackJackPlayer = new BlackJackPlayer(userData, user);
                if (table.IsGameActive)
                {
                    logger.LogInformation("Adding {player} to the Pending Players collection", blackJackPlayer.Name);
                    table.PendingPlayers.Add(blackJackPlayer);
                }
                else
                {
                    logger.LogInformation("Adding {player} to the Players collection", blackJackPlayer.Name);
                    table.Players.Add(blackJackPlayer);
                }
                return true;
            }
            logger.LogInformation("{player} is already part of the game!", user.Username);
            return false;
        }

        /// <summary>
        ///     Safely removes a <see cref="IUser"/>
        /// </summary>
        /// <param name="table">The table to remove the user from</param>
        /// <param name="user">A reference to the Discord <see cref="IUser"/></param>
        /// <returns>A promise that, if true, means the player was removed successfully</returns>
        public bool RemovePlayerSafelyFromTable(BlackJackTable table, IUser user)
        {
            if (table.IsGameActive && !table.LeavingPlayers.Any(lp => lp.User.UserId == user.Id))
            {
                logger.LogInformation("Adding {player} to the Leaving Players collection", user.Username);
                table.LeavingPlayers.Add(table.Players.First(p => p.User.UserId == user.Id));
                return true;
            }
            if (!table.IsGameActive && table.Players.Any(p => p.User.UserId == user.Id))
            {
                logger.LogInformation("Removing {player}...", user.Username);
                table.Players.Remove(table.Players.First(p => p.User.UserId == user.Id));
                return true;
            }
            return false;
        }


        #region Helper Methods

        private void DealCardsToPlayers(BlackJackTable table)
        {
            logger.LogTrace("Dealing out hands to {count} players", table.Players.Count);
            for (var c = 0; c < 2; c++)
            {
                foreach (var player in table.Players)
                {
                    var card = table.Deck.GetNextCard();
                    logger.LogTrace("{player} got {card}", player.Name, card.ToString());
                    player.Hand.Cards.Add(card);
                }
                var dealerCard = table.Deck.GetNextCard();
                logger.LogTrace("{player} got {card}", table.Dealer.Name, dealerCard.ToString());
                table.Dealer.Hand.Cards.Add(dealerCard);
            }
        }

        private void HandleTableLevelInteractions(BlackJackTable table, IThreadChannel thread)
        {
            var threadId = thread.Id;
            logger.LogInformation("Attaching interaction callbacks for thread {threadId}", thread.Id);
            async Task<bool> CanPlayerChangeCurrentBet(
                BlackJackTable currentTable,
                SocketMessageComponent smc)
            {
                if (!currentTable.CanPlayerAlterBet(smc.User.Id))
                {
                    await smc.RespondAsync("Sorry, you can't change your bet right now!", ephemeral: true);
                    return false;
                }
                var player = currentTable.FindPlayer(smc.User.Id);
                if (player is null)
                {
                    await smc.RespondAsync("Sorry, I couldn't find you at the table... which is strange. You should report this as a bug to the maintainer", ephemeral: true);
                    return false;
                }
                return true;
            }
            async Task HandleChangingPlayerBet(
                BlackJackTable currentTable,
                SocketMessageComponent smc,
                Action<BlackJackPlayer> amountSetterAction)
            {
                if (await CanPlayerChangeCurrentBet(currentTable, smc))
                {
                    var player = currentTable.FindPlayer(smc.User.Id);
                    var currencyData = currencyService.GetOrCreateCurrencyData(player.User);

                    var oldBet = player.CurrentBet;
                    amountSetterAction(player);
                    player.CurrentBet = Math.Max(0, player.CurrentBet);
                    var newBet = player.CurrentBet;
                    logger.LogTrace("Changing bet from {old bet} to {new bet} for user {userName} {userId}", oldBet, newBet, player.Name, player.User.UserId);

                    if (currencyData.Currency > player.CurrentBet)
                    {
                        await smc.RespondAsync($"Your bet has been set to {player.CurrentBet}. However, that's more than you currently can afford (your currency is {currencyData.Currency}).", ephemeral: true);
                    }
                    else
                    {
                        await smc.RespondAsync($"Your bet has been set to {player.CurrentBet}", ephemeral: true);
                    }
                }
            }
            interactionHandlingService.RegisterCallbackHandler($"join-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                logger.LogTrace("Join event for {threadId}", threadId);
                if (await AddPlayerSafelyToTable(table, smc.User))
                {
                    await thread.AddUserAsync(smc.User as IGuildUser);
                    await smc.RespondAsync($"Welcome to the table {smc.User.Username}! Here are a few preset Bid buttons to interact with. Alternately you set your Bid directly with `/blackjack bid <amount>` to set your Bid to any number",
                        ephemeral: true, components: GetBidButtonComponents(threadId).Build());
                }
                else
                {
                    await smc.RespondAsync("Couldn't add you to the table. Have you already joined?");
                }
            }));
            interactionHandlingService.RegisterCallbackHandler($"leave-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                logger.LogTrace("Leave event for {threadId}", threadId);
                if (RemovePlayerSafelyFromTable(table, smc.User))
                {
                    await smc.RespondAsync($"Your removal has been scheduled. If a round is currently going you will be removed at the end of the round.", ephemeral: true);
                }
                else
                {
                    await smc.RespondAsync("Couldn't remove you from the table. Have you already left?", ephemeral: true);
                }
            }));
            interactionHandlingService.RegisterCallbackHandler($"bid-1-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                await HandleChangingPlayerBet(table, smc, bjp => bjp.CurrentBet = 1);
            }));
            interactionHandlingService.RegisterCallbackHandler($"bid-5-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                await HandleChangingPlayerBet(table, smc, bjp => bjp.CurrentBet = 5);
            }));
            interactionHandlingService.RegisterCallbackHandler($"bid-10-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                await HandleChangingPlayerBet(table, smc, bjp => bjp.CurrentBet = 10);
            }));
            interactionHandlingService.RegisterCallbackHandler($"bid-add-5-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                await HandleChangingPlayerBet(table, smc, bjp => bjp.CurrentBet += 5);
            }));
            interactionHandlingService.RegisterCallbackHandler($"bid-rem-5-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                await HandleChangingPlayerBet(table, smc, bjp => bjp.CurrentBet -= 5);
            }));
            interactionHandlingService.RegisterCallbackHandler($"hand-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (table.IsGameActive)
                {
                    var player = table.FindPlayer(smc.User.Id);
                    if (player is null)
                    {
                        await smc.RespondAsync("You don't have a hand to see currently!", ephemeral: true);
                    }
                    else
                    {
                        await smc.RespondWithFileAsync(await player.Hand.GetHandAsAttachment(), ephemeral: true);
                    }
                }
            }));
        }

        private void RemoveTableLevelInteractions(ulong threadId)
        {
            logger.LogInformation("Removing interaction callbacks for {threadId}", threadId);
            interactionHandlingService.RemoveButtonCallbacks(
                $"join-{threadId}",
                $"leave-{threadId}",
                $"bid-1-{threadId}",
                $"bid-5-{threadId}",
                $"bid-10-{threadId}",
                $"bid-add-5-{threadId}",
                $"bid-add-10-{threadId}",
                $"hand-{threadId}");
        }

        private async Task CreateInitialMessagesAndPinThem(BlackJackTableDetails blackJackTableDetails)
        {
            var thread = blackJackTableDetails.ThreadChannel;
            var pinned = await thread.GetPinnedMessagesAsync();
            if (!pinned.Any())
            {
                logger.LogInformation("Creating initial pinned messages for thread {threadId}", thread.Id);
                var welcomeMsg = await thread.SendMessageAsync($"Welcome to Table '{thread.Name}'. Please use these two buttons to Join or Leave the game. Alternatively, you can also use `/blackjack join` and `/blackjack leave` inside this thread to join and leave",
                    components: GetJoinAndLeaveComponents(thread.Id).Build());
                var betMsg = await thread.SendMessageAsync("Once you have joined the table, you can set your bet by typing `/blackjack bet <amount>` to manually specify your amount OR you can use these quick bet options",
                    components: GetBidButtonComponents(thread.Id).Build());
                await welcomeMsg.PinAsync();
                await betMsg.PinAsync();
            }
        }

        private void HandlePreGameStartup(BlackJackTable table)
        {
            HandleJoiningUsers(table);
            HandleLeavingUsers(table);
        }

        private void HandlePostGameCleanUp(BlackJackTable table)
        {
            HandleLeavingUsers(table);
            CleanUpHands(table);
        }

        private void CleanUpHands(BlackJackTable table)
        {
            logger.LogInformation("Cleaning up the Player & Dealer Hands");
            foreach(var player in table.Players
                .Union(table.PendingPlayers)
                .Union(table.LeavingPlayers))
            {
                player.Hand.Cards.Clear();
            }
            table.Dealer.Hand.Cards.Clear();
        }

        private void HandleJoiningUsers(BlackJackTable table)
        {
            foreach(var pending in table.PendingPlayers)
            {
                if (!table.Players.Any(p => p.User.UserId == pending.User.UserId))
                {
                    logger.LogInformation("Adding {player} to the Players collection", pending.Name);
                    table.Players.Add(pending);
                }
            }
            table.PendingPlayers.Clear();
        }

        private void HandleLeavingUsers(BlackJackTable table)
        {
            for (var playerIndex = table.Players.Count - 1; playerIndex >= 0; playerIndex--)
            {
                var currentPlayer = table.Players[playerIndex];
                if (table.LeavingPlayers.Any(lp => lp.User.UserId == currentPlayer.User.UserId))
                {
                    logger.LogInformation("Removing {player} from the Players collection", currentPlayer.Name);
                    table.Players.RemoveAt(playerIndex);
                }
            }
        }

        private static IEnumerable<BlackJackPlayer> GetZeroBetPlayers(BlackJackTable table)
        {
            return table.Players.Where(p => p.CurrentBet == 0);
        }

        private async Task HandleGameStartup(IThreadChannel thread, BlackJackTable table)
        {
            logger.LogInformation("Beginning game for thread {threadId}", thread.Id);
            await thread.SendMessageAsync("A round of BlackJack is going to begin shortly. For all players joining, please ensure your bets are in within the next thirty seconds.\r\nThere will be a fifteen second warning that will include players who have not set their bets yet!", components: GetJoinAndLeaveComponents(thread.Id).Build());
            await Task.Delay(TimeSpan.FromSeconds(15));
            var zeroBetPlayers = GetZeroBetPlayers(table).ToList();
            if (zeroBetPlayers.Any())
            {
                await thread.SendMessageAsync($"The game will begin in fifteen seconds. The following players have not set their bets yet: {GetCommaSeparatedUserNames(zeroBetPlayers)}", components: GetBidButtonComponents(thread.Id).Build());
            }
            else
            {
                await thread.SendMessageAsync("The game will begin in fifteen seconds");
            }
            await Task.Delay(TimeSpan.FromSeconds(15));
        }

        private async Task MoveAndNotifyZeroBetAndBrokePlayers(IThreadChannel thread, BlackJackTable table)
        {
            var moved = new List<BlackJackPlayer>();
            for (var playerIndex = table.Players.Count - 1; playerIndex >= 0; playerIndex--)
            {
                var currentPlayer = table.Players[playerIndex];
                var playerCurrency = currencyService.GetOrCreateCurrencyData(currentPlayer.User);
                currentPlayer.CurrentBet = Math.Min(currentPlayer.CurrentBet, playerCurrency.Currency);
                if (currentPlayer.CurrentBet == 0 || playerCurrency.Currency < currentPlayer.CurrentBet)
                {
                    logger.LogInformation("Player {player} is being moved to Pending Players as they are not ready to play yet!", currentPlayer.Name);
                    moved.Add(currentPlayer);
                    table.Players.RemoveAt(playerIndex);
                    table.PendingPlayers.Add(currentPlayer);
                }
            }
            await thread.SendMessageAsync($"The following players will not be playing this round but can still set their bet: {GetCommaSeparatedUserNames(moved)}");
        }

        private async Task HandlePlayerHand(IThreadChannel thread, BlackJackTable table, BlackJackPlayer player)
        {
            await HandlePlayerBettingInteractions(thread, table, player);
            RemovePlayerBettingInteractions(thread, player);
        }

        private static async Task HandleDealerHand(IThreadChannel thread, BlackJackTable table, BlackJackPlayer dealer)
        {
            var hand = await ShowHandToChannel(thread, dealer);
            await Task.Delay(TimeSpan.FromSeconds(2));
            while (dealer.Hand.Value < 17)
            {
                var card = table.Deck.GetNextCard();
                dealer.Hand.Cards.Add(card);
                var newHand = await dealer.Hand.GetHandAsAttachment();
                await hand.ModifyAsync(properties =>
                {
                    properties.Attachments = new[] { newHand };
                    properties.Content = $"{dealer.Name}'s is showing {dealer.Hand.Value} total.";
                });
                await Task.Delay(TimeSpan.FromSeconds(2));
                if (dealer.Hand.IsBust)
                {
                    await thread.SendMessageAsync($"{dealer.Name} has bust!");
                }
            }

            if (!dealer.Hand.IsBust)
            {
                await thread.SendMessageAsync($"{dealer.Name} has finished their turn and ended with a hand of {dealer.Hand.Value}");
            }
        }

        private async Task HandleScoreCalculation(IThreadChannel thread, BlackJackTable table)
        {
            var winners = table.Players.Where(player =>
                (player.Hand.Value > table.Dealer.Hand.Value && !player.Hand.IsBust) ||
                    player.Hand.IsBlackJack ||
                    (table.Dealer.Hand.IsBust && !player.Hand.IsBust)).ToList();

            var losers = table.Players.Where(player =>
                (player.Hand.Value < table.Dealer.Hand.Value && !table.Dealer.Hand.IsBust) ||
                (player.Hand.IsBust && !table.Dealer.Hand.IsBust)).ToList();

            var neutrals = table.Players.Where(player =>
                (player.Hand.Value == table.Dealer.Hand.Value && !player.Hand.IsBust) ||
                (player.Hand.IsBust && table.Dealer.Hand.IsBust)).ToList();

            var roundResultBuilder = new StringBuilder();
            roundResultBuilder.AppendLine($"Winners: {GetCommaSeparatedUserNames(winners)}");
            roundResultBuilder.AppendLine($"Losers: {GetCommaSeparatedUserNames(losers)}");
            roundResultBuilder.AppendLine($"Neutrals: {GetCommaSeparatedUserNames(neutrals)}");
            roundResultBuilder.AppendLine();

            foreach (var winner in winners)
            {
                var winnerCurrency = currencyService.GetOrCreateCurrencyData(winner.User);
                var winnings = winner.Hand.IsBlackJack ? (ulong)Math.Round(winner.CurrentBet * 1.5) : winner.CurrentBet;
                roundResultBuilder.AppendLine($"{winner.Name}: {winnings}");
            }

            foreach (var loser in losers)
            {
                var loserCurrency = currencyService.GetOrCreateCurrencyData(loser.User);
                loserCurrency.Currency -= loser.CurrentBet;
                roundResultBuilder.AppendLine($"{loser.Name}: -{loser.CurrentBet}");
            }
            await thread.SendMessageAsync(roundResultBuilder.ToString());
        }

        private static async Task<IUserMessage> ShowHandToChannel(
            IThreadChannel thread,
            BlackJackPlayer player,
            string message = null,
            MessageComponent component = null,
            bool hideFirstCard = false)
        {
            return await thread.SendFileAsync(
                await player.Hand.GetHandAsAttachment(hideFirstCard),
                message ?? $"{player.Name}'s is showing {(hideFirstCard ? player.Hand.MaskedValue(1) : player.Hand.Value)} total.",
                components: component);
        }

        private async Task HandlePlayerBettingInteractions(IThreadChannel thread, BlackJackTable table, BlackJackPlayer player)
        {
            var threadId = thread.Id;
            var playerId = player.User.UserId;
            var hasProcessedBust = false;
            var hasStood = false;

            var playerHand = await ShowHandToChannel(thread, player,
                message: $"It is now {player.DiscordUser.Mention}'s turn! Their hand currently showing {player.Hand.Value}.",
                component: GetHandComponents(thread.Id, player).Build());

            interactionHandlingService.RegisterCallbackHandler($"hit-{threadId}-{playerId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (smc.User.Id != playerId)
                {
                    await smc.DeferAsync();
                    return;
                }
                var card = table.Deck.GetNextCard();
                await smc.DeferAsync();
                await Task.Delay(TimeSpan.FromSeconds(1));
                player.Hand.Cards.Add(card);

                var handToShow = await player.Hand.GetHandAsAttachment();
                var content = $"Next card: {card}! {player.DiscordUser.Mention}'s is showing {player.Hand.Value} total.";
                var components = GetHandComponents(thread.Id, player).Build();

                if (player.Hand.IsBust)
                {
                    content += " Bust!";
                    components = null;
                    hasProcessedBust = true;
                }
                await playerHand.ModifyAsync(properties =>
                {
                    properties.Attachments = new[] { handToShow };
                    properties.Components = components;
                    properties.Content = content;
                });
            }));
            interactionHandlingService.RegisterCallbackHandler($"stand-{threadId}-{playerId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (smc.User.Id != playerId)
                {
                    await smc.DeferAsync();
                    return;
                }
                var handToShow = await player.Hand.GetHandAsAttachment();
                await playerHand.ModifyAsync(properties =>
                {
                    properties.Attachments = new[] { handToShow };
                    properties.Components = null;
                    properties.Content = $"{player.DiscordUser.Mention} is standing with a hand total of {player.Hand.Value}.";
                });
                await Task.Delay(TimeSpan.FromSeconds(1));
                hasStood = true;
            }));
            interactionHandlingService.RegisterCallbackHandler($"split-{threadId}-{playerId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (smc.User.Id != playerId)
                {
                    await smc.DeferAsync();
                    return;
                }
                var newHand = new Hand();
                newHand.Cards.Add(player.Hand.Cards[1]);
                newHand.Cards.Add(table.Deck.GetNextCard());
                player.Hand.Cards.RemoveAt(1);
                player.Hand.Cards.Add(table.Deck.GetNextCard());
                var temporaryPlayer = BlackJackPlayer.CreateSplit(player, newHand);
                table.InsertSplitPlayerOntoStack(temporaryPlayer);
                var handToShow = await player.Hand.GetHandAsAttachment();
                await playerHand.ModifyAsync(properties =>
                {
                    properties.Attachments = new[] { handToShow };
                    properties.Components = GetHandComponents(thread.Id, player).Build();
                    properties.Content = $"{player.DiscordUser.Mention} split their hand! Their current hand is showing {player.Hand.Value} total.";
                });
            }));

            SpinWait.SpinUntil(() => (player.Hand.IsBust && hasProcessedBust) || hasStood);
        }

        private void RemovePlayerBettingInteractions(IThreadChannel thread, BlackJackPlayer player)
        {
            var threadId = thread.Id;
            var playerId = player.User.UserId;
            interactionHandlingService.RemoveButtonCallbacks($"hit-{threadId}-{playerId}", $"stand-{threadId}-{playerId}", $"split-{threadId}-{playerId}");
        }


        public static ComponentBuilder GetBidButtonComponents(ulong threadId)
        {
            return new ComponentBuilder()
                .WithButton("Bid 1", $"bid-1-{threadId}")
                .WithButton("Bid 5", $"bid-5-{threadId}")
                .WithButton("Bid 10", $"bid-10-{threadId}")
                .WithButton("Bid +5", $"bid-add-5-{threadId}")
                .WithButton("Bid -5", $"bid-rem-5-{threadId}");
        }

        private static ComponentBuilder GetJoinAndLeaveComponents(ulong threadId)
        {
            return new ComponentBuilder()
                .WithButton("Join", $"join-{threadId}")
                .WithButton("Leave", $"leave-{threadId}");
        }

        private static ComponentBuilder GetHandViewComponent(ulong threadId)
        {
            return new ComponentBuilder()
                .WithButton("See Hand", $"hand-{threadId}");
        }

        private static ComponentBuilder GetHandComponents(ulong threadId, BlackJackPlayer player)
        {
            var cb = GetHandViewComponent(threadId)
                .WithButton("Hit", $"hit-{threadId}-{player.User.UserId}")
                .WithButton("Stand", $"stand-{threadId}-{player.User.UserId}");
            if (player.Hand.IsSplittable)
            {
                cb = cb.WithButton("Split", $"split-{threadId}-{player.User.UserId}");
            }
            return cb;
        }

        private static bool IsTableReadyToPlay(BlackJackTable table) => table.Players.Any();

        private static bool HasPendingPlayers(BlackJackTable table) => table.PendingPlayers.Any();

        private static string GetCommaSeparatedUserNames(List<BlackJackPlayer> zeroBetPlayers)
        {
            return string.Join(", ", zeroBetPlayers.Select(p => p.Name));
        }

        #endregion Helper Methods



        private sealed class BlackJackTableDetails
        {
            public BlackJackTable Table { get; }

            public IThreadChannel ThreadChannel { get; }

            public bool IsThreadCurrentlyRunning { get; set; } = false;

            public CancellationTokenSource CancellationTokenSource { get; set; } = new();


            public BlackJackTableDetails(BlackJackTable table, IThreadChannel threadChannel)
            {
                Table = table;
                ThreadChannel = threadChannel;
            }
        }

    }
}
