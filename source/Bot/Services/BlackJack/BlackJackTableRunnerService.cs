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
        private readonly InteractionHandlingService interactionHandlingService;
        private readonly ILogger<BlackJackTableRunnerService> logger;
        private readonly UserService userService;

        public BlackJackTableRunnerService(
            CancellationTokenSource cancellationTokenSource,
            CurrencyService currencyService,
            InteractionHandlingService interactionHandlingService,
            ILogger<BlackJackTableRunnerService> logger,
            UserService userService)
        {
            this.currencyService = currencyService ?? throw new ArgumentNullException(nameof(currencyService));
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
                var deck = Deck.CreateDeck(4);
                var bjt = new BlackJackTable(deck);
                tables.TryAdd(threadChannel.Id, new(bjt, threadChannel));
                return bjt;
            }
            return t.Table;
        }

        /// <summary>
        ///     Starts the thread 
        /// </summary>
        /// <param name="threadChannel"></param>
        public bool StartBlackJackTableForChannel(IThreadChannel threadChannel)
        {
            if (!tables.TryGetValue(threadChannel.Id, out var details)) return false;
            if (details.IsThreadCurrentlyRunning) return true;
            ThreadPool.QueueUserWorkItem(async bjtd => { await TableRunnerLoop(bjtd); }, details, false);
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
                t.CancellationTokenSource.Cancel();
                tables.Remove(threadId, out _);
            }
        }


        private async Task TableRunnerLoop(BlackJackTableDetails blackJackTableDetails)
        {
            // I'm lazy
            var thread = blackJackTableDetails.ThreadChannel;
            var table = blackJackTableDetails.Table;
            var token = blackJackTableDetails.CancellationTokenSource.Token;

            // Hook up table level Interaction Buttons
            HandleTableLevelInteractions(table, thread.Id);
            await CreateInitialMessagesAndPinThem(blackJackTableDetails);
            blackJackTableDetails.IsThreadCurrentlyRunning = true;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    SpinWait.SpinUntil(() => IsTableReadyToPlay(table) || HasPendingPlayers(table));
                    HandlePreGameStartup(table);
                    token.ThrowIfCancellationRequested();
                    await HandleGameStartup(thread, table);
                    await MoveAndNotifyZeroBetAndBrokePlayers(thread, table);
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

                    while (table.GetNextPlayer(out var currentPlayer))
                    {
                        token.ThrowIfCancellationRequested();
                        await HandlePlayerHand(thread, table, currentPlayer);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }

                    token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    await HandleDealerHand(thread, table, table.Dealer);
                    await HandleScoreCalculation(thread, table);
                    HandlePostGameCleanUp(table);
                    table.IsGameActive = false;
                    await thread.SendMessageAsync("The round of BlackJack has concluded. The table will take a short 30 second pause before beginning a new round!");
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
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


        #region Helper Methods

        private static void DealCardsToPlayers(BlackJackTable table)
        {
            for (var c = 0; c < 2; c++)
            {
                foreach (var player in table.Players)
                {
                    player.Hand.Cards.Add(table.Deck.GetNextCard());
                }
                table.Dealer.Hand.Cards.Add(table.Deck.GetNextCard());
            }
        }

        private void HandleTableLevelInteractions(BlackJackTable table, ulong threadId)
        {
            static async Task<bool> CanPlayerChangeCurrentBet(
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
            static async Task HandleChangingPlayerBet(
                BlackJackTable currentTable,
                SocketMessageComponent smc,
                Action<BlackJackPlayer> amountSetterAction)
            {
                if (await CanPlayerChangeCurrentBet(currentTable, smc))
                {
                    var player = currentTable.FindPlayer(smc.User.Id);
                    amountSetterAction(player);
                    await smc.RespondAsync($"Your bet has been set to {player.CurrentBet}", ephemeral: true);
                }
            }
            interactionHandlingService.RegisterCallbackHandler($"join-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (await AddPlayerSafelyToTable(table, smc.User))
                {
                    await smc.RespondAsync($"Welcome to the table {smc.User.Username}! Here are a few preset Bid buttons to interact with. Alternately you set your Bid directly with `/blackjack bid <amount>` to set your Bid to any number",
                        ephemeral: true, components: GetBidButtonComponents(threadId).Build());
                }
            }));
            interactionHandlingService.RegisterCallbackHandler($"leave-{threadId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (RemovePlayerSafelyFromTable(table, smc.User))
                {
                    await smc.RespondAsync($"You have been removed from the table! Thank you for playing.", ephemeral: true);
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

        private static async Task CreateInitialMessagesAndPinThem(BlackJackTableDetails blackJackTableDetails)
        {
            var thread = blackJackTableDetails.ThreadChannel;
            var pinned = await thread.GetPinnedMessagesAsync();
            if (!pinned.Any())
            {
                var welcomeMsg = await thread.SendMessageAsync($"Welcome to Table '{thread.Name}'. Please use these two buttons to Join or Leave the game. Alternatively, you can also use `/blackjack join` and `/blackjack leave` inside this thread to join and leave",
                    components: GetJoinAndLeaveComponents(thread.Id).Build());
                var betMsg = await thread.SendMessageAsync("Once you have joined the table, you can set your bet by typing `/blackjack bet <amount>` to manually specify your amount OR you can use these quick bet options",
                    components: GetBidButtonComponents(thread.Id).Build());
                await welcomeMsg.PinAsync();
                await betMsg.PinAsync();
            }
        }

        public async Task<bool> AddPlayerSafelyToTable(BlackJackTable table, IUser user)
        {
            if (!table.PendingPlayers.Any(pp => pp.User.UserId == user.Id) &&
                !table.Players.Any(p => p.User.UserId == user.Id))
            {
                var userData = await userService.GetOrCreateUserData(user);
                var blackJackPlayer = new BlackJackPlayer(userData, user);
                if (table.IsGameActive)
                {
                    table.PendingPlayers.Add(blackJackPlayer);
                }
                else
                {
                    table.Players.Add(blackJackPlayer);
                }
                return true;
            }
            return false;
        }

        public static bool RemovePlayerSafelyFromTable(BlackJackTable table, IUser user)
        {
            if (table.IsGameActive && !table.LeavingPlayers.Any(lp => lp.User.UserId == user.Id))
            {
                table.LeavingPlayers.Add(table.Players.First(p => p.User.UserId == user.Id));
                return true;
            }
            if (!table.IsGameActive && table.Players.Any(p => p.User.UserId == user.Id))
            {
                table.Players.Remove(table.Players.First(p => p.User.UserId == user.Id));
                return true;
            }
            return false;
        }

        private static ComponentBuilder GetBidButtonComponents(ulong threadId)
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

        private static void HandlePreGameStartup(BlackJackTable table)
        {
            HandleJoiningUsers(table);
            HandleLeavingUsers(table);
        }

        private static void HandlePostGameCleanUp(BlackJackTable table)
        {
            HandleLeavingUsers(table);
            CleanUpHands(table);
        }

        private static void CleanUpHands(BlackJackTable table)
        {
            foreach(var player in table.Players
                .Union(table.PendingPlayers)
                .Union(table.LeavingPlayers))
            {
                player.Hand.Cards.Clear();
            }
            table.Dealer.Hand.Cards.Clear();
        }

        private static void HandleJoiningUsers(BlackJackTable table)
        {
            foreach(var pending in table.PendingPlayers)
            {
                if (!table.Players.Any(p => p.User.UserId == pending.User.UserId))
                {
                    table.Players.Add(pending);
                }
            }
            table.PendingPlayers.Clear();
        }

        private static void HandleLeavingUsers(BlackJackTable table)
        {
            for (var playerIndex = table.Players.Count - 1; playerIndex >= 0; playerIndex--)
            {
                var currentPlayer = table.Players[playerIndex];
                if (table.LeavingPlayers.Any(lp => lp.User.UserId == currentPlayer.User.UserId))
                {
                    table.Players.RemoveAt(playerIndex);
                }
            }
        }

        private static IEnumerable<BlackJackPlayer> GetZeroBetPlayers(BlackJackTable table)
        {
            return table.Players.Where(p => p.CurrentBet == 0);
        }

        private static async Task HandleGameStartup(IThreadChannel thread, BlackJackTable table)
        {
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
                if (currentPlayer.CurrentBet == 0 || playerCurrency.Currency < currentPlayer.CurrentBet)
                {
                    moved.Add(currentPlayer);
                    table.Players.RemoveAt(playerIndex);
                    table.PendingPlayers.Add(currentPlayer);
                }
            }
            await thread.SendMessageAsync($"The following players will not be playing this round but can still set their bet: {GetCommaSeparatedUserNames(moved)}");
        }

        private async Task HandlePlayerHand(IThreadChannel thread, BlackJackTable table, BlackJackPlayer player)
        {
            await ShowHandToChannel(thread, player,
                message: $"It is now {player.Name}'s turn! Their hand currently showing {player.Hand.Value}.",
                component: GetHandComponents(thread.Id, player).Build());
            await HandlePlayerBettingInteractions(thread, table, player);
            RemovePlayerBettingInteractions(thread, player);
        }

        private static async Task HandleDealerHand(IThreadChannel thread, BlackJackTable table, BlackJackPlayer dealer)
        {
            await ShowHandToChannel(thread, dealer);
            await Task.Delay(TimeSpan.FromSeconds(2));
            while (dealer.Hand.Value < 17)
            {
                var card = table.Deck.GetNextCard();
                dealer.Hand.Cards.Add(card);
                await ShowHandToChannel(thread, dealer, $"{dealer.Name} takes a hit! It is a {card}! {dealer.Name} is now showing {dealer.Hand.Value}");
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

        private static async Task ShowHandToChannel(
            IThreadChannel thread,
            BlackJackPlayer player,
            string message = null,
            MessageComponent component = null,
            bool hideFirstCard = false)
        {
            await thread.SendFileAsync(
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

            interactionHandlingService.RegisterCallbackHandler($"hit-{threadId}-{playerId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (smc.User.Id != playerId)
                {
                    await smc.RespondAsync("This interaction isn't for you!", ephemeral: true);
                    return;
                }
                var card = table.Deck.GetNextCard();
                await smc.RespondAsync($"{player.Name} takes a hit! It is a {card}!");
                await Task.Delay(TimeSpan.FromSeconds(1));
                player.Hand.Cards.Add(card);
                if (player.Hand.IsBust)
                {
                    await ShowHandToChannel(thread, player, message: $"Unfortunately, {player.Name}'s hand has bust with a value of {player.Hand.Value}!", component: GetHandComponents(thread.Id, player).Build());
                    hasProcessedBust = true;
                }
                else
                {
                    await ShowHandToChannel(thread, player, component: GetHandComponents(thread.Id, player).Build());
                }
            }));
            interactionHandlingService.RegisterCallbackHandler($"stand-{threadId}-{playerId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (smc.User.Id != playerId)
                {
                    await smc.RespondAsync("This interaction isn't for you!", ephemeral: true);
                    return;
                }
                await smc.RespondAsync($"{player.Name} stands!");
                hasStood = true;
            }));
            interactionHandlingService.RegisterCallbackHandler($"split-{threadId}-{playerId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (smc.User.Id != playerId)
                {
                    await smc.RespondAsync("This interaction isn't for you!", ephemeral: true);
                    return;
                }
                await smc.RespondAsync($"{player.Name} is going to split their hand!");
                var newHand = new Hand();
                newHand.Cards.Add(player.Hand.Cards[1]);
                player.Hand.Cards.RemoveAt(1);
                var temporaryPlayer = BlackJackPlayer.CreateSplit(player, newHand);
                table.InsertSplitPlayerOntoStack(temporaryPlayer);
                await ShowHandToChannel(thread, player, component: GetHandComponents(thread.Id, player).Build());
            }));

            SpinWait.SpinUntil(() => (player.Hand.IsBust && hasProcessedBust) || hasStood);

            if (!player.Hand.IsBust)
            {
                await thread.SendMessageAsync($"{player.Name} has finished their turn and ended with a hand of {player.Hand.Value}");
            }
        }

        private void RemovePlayerBettingInteractions(IThreadChannel thread, BlackJackPlayer player)
        {
            var threadId = thread.Id;
            var playerId = player.User.UserId;
            interactionHandlingService.RemoveButtonCallbacks($"hit-{threadId}-{playerId}", $"stand-{threadId}-{playerId}", $"split-{threadId}-{playerId}");
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
