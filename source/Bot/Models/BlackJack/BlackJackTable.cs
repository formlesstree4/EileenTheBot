using Bot.Services;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Models.BlackJack
{


    public sealed class BlackJackTable
    {

        private readonly Stack<BlackJackPlayer> currentRoundPlayers = new();
        private readonly Queue<BlackJackPlayer> finishedRoundPlayers = new();


        /// <summary>
        ///     Gets the unique Table ID
        /// </summary>
        /// <remarks>
        ///     This may not even stick around; it's really not very useful
        /// </remarks>
        public Guid TableId { get; } = Guid.NewGuid();

        /// <summary>
        ///     Gets whether or not the table is currently playing a game.
        /// </summary>
        public bool IsGameActive { get; set; } = false;

        /// <summary>
        ///     Gets the Dealer for this table
        /// </summary>
        public BlackJackPlayer Dealer { get; } = new(null, null);

        /// <summary>
        ///     Gets the list of current active players
        /// </summary>
        public List<BlackJackPlayer> Players { get; } = new();

        /// <summary>
        ///     Gets the list of players that are waiting to join on the next round.
        /// </summary>
        public List<BlackJackPlayer> PendingPlayers { get; } = new();

        /// <summary>
        ///     Gets the list of players that are waiting to leave at the end of the round.
        /// </summary>
        public List<BlackJackPlayer> LeavingPlayers { get; } = new();

        /// <summary>
        ///     Gets the <see cref="Deck"/> used at the table.
        /// </summary>
        public Deck Deck { get; private set; }



        /// <summary>
        ///     Creates a new <see cref="BlackJackTable"/>
        /// </summary>
        /// <param name="deck">The Deck of cards to use indefinitely for this table</param>
        public BlackJackTable(Deck deck)
        {
            Deck = deck;
        }



        /// <summary>
        ///     Returns whether or not the given <see cref="BlackJackPlayer"/> can alter their bet
        /// </summary>
        /// <param name="player"><see cref="BlackJackPlayer"/></param>
        /// <returns>true/false if their Bet can be changed</returns>
        public bool CanPlayerAlterBet(BlackJackPlayer player)
        {
            return CanPlayerAlterBet(player.User.UserId);
        }

        /// <summary>
        ///     Returns whether or not the given player ID can alter their bet
        /// </summary>
        /// <param name="playerId">The Discord snowflake ID</param>
        /// <returns>true/false if their Bet can be changed</returns>
        public bool CanPlayerAlterBet(ulong playerId)
        {
            return IsGameActive
                ? PendingPlayers.Any(c => c.User.UserId == playerId) || currentRoundPlayers.Any(c => c.User.UserId == playerId)
                : PendingPlayers.Any(c => c.User.UserId == playerId) || Players.Any(c => c.User.UserId == playerId);
        }

        /// <summary>
        ///     Gets the next player in the round to go
        /// </summary>
        /// <returns>If true, <paramref name="nextPlayer"/> is a Player. If false, <paramref name="nextPlayer"/> is the Dealer</returns>
        public bool GetNextPlayer(out BlackJackPlayer nextPlayer)
        {
            if (currentRoundPlayers.Count == 0)
            {
                nextPlayer = Dealer;
                return false;
            }
            nextPlayer = currentRoundPlayers.Pop();
            finishedRoundPlayers.Enqueue(nextPlayer);
            return true;
        }

        /// <summary>
        /// Looks for a <see cref="BlackJackPlayer"/> that's either pending to play or playing
        /// </summary>
        /// <param name="playerId">The Player ID to look for</param>
        /// <returns><see cref="BlackJackPlayer"/> if discovered</returns>
        public BlackJackPlayer FindPlayer(ulong playerId)
        {
            return PendingPlayers.FirstOrDefault(c => c.User.UserId == playerId) ??
                Players.FirstOrDefault(c => c.User.UserId == playerId);
        }

        /// <summary>
        ///     Adds a Player, who is a clone of another Player, to the Stack of Players
        /// </summary>
        /// <param name="splitHandPlayer"><see cref="BlackJackPlayer"/></param>
        public void InsertSplitPlayerOntoStack(BlackJackPlayer splitHandPlayer)
        {
            currentRoundPlayers.Push(splitHandPlayer);
        }

        /// <summary>
        ///     Gets the <see cref="BlackJackPlayer"/> in the order they played the round in so the post-round processing can occur.
        /// </summary>
        /// <returns>A collection of <see cref="BlackJackPlayer"/></returns>
        /// <remarks>This is not multi-iterable friendly</remarks>
        public IEnumerable<BlackJackPlayer> GetPlayersForEndOfRoundProcessing()
        {
            while(finishedRoundPlayers.Count > 0)
            {
                yield return finishedRoundPlayers.Dequeue();
            }
        }

        /// <summary>
        ///     Sets up the table internally for the next round
        /// </summary>
        /// <remarks>
        ///     The Runner Service is responsible for the manipulation of <see cref="PendingPlayers"/> and <see cref="LeavingPlayers"/>
        /// </remarks>
        public void SetupTable()
        {
            foreach(var player in currentRoundPlayers.Reverse())
            {
                currentRoundPlayers.Push(player);
            }
        }

    }

    /*
    /// <summary>
    ///     Represents an active table of BlackJack. It is a simple object model that the service will use to 
    /// </summary>
    public sealed class BlackJackTable
    {

        private bool isGameLoopRunning = false;
        private CancellationTokenSource cancellationTokenSource;
        private readonly DiscordSocketClient discordSocketClient;
        private readonly CurrencyService currencyService;
        private readonly InteractionHandlingService interactionHandlingService;
        private readonly IThreadChannel threadChannel;


        /// <summary>
        ///     Gets the thread where this table is hosted at
        /// </summary>
        public ulong ThreadId => threadChannel.Id;

        /// <summary>
        ///     Gets the unique game ID
        /// </summary>
        public Guid GameId { get; }

        /// <summary>
        ///     Gets or sets if a game is currently active on this table.
        /// </summary>
        public bool IsGameActive { get; private set; } = false;

        /// <summary>
        ///     Gets the Dealer for this table
        /// </summary>
        public BlackJackPlayer Dealer { get; } = new(null);

        /// <summary>
        ///     Gets the players for this BlackJack table
        /// </summary>
        public List<BlackJackPlayer> Players { get; } = new();

        /// <summary>
        ///     Gets the Players that will join in the next round
        /// </summary>
        public List<EileenUserData> Pending { get; } = new();

        /// <summary>
        ///     Gets the Players that will leave at the end of the round
        /// </summary>
        public List<EileenUserData> Leaving { get; } = new();

        /// <summary>
        ///     Gets the current Deck used for this table
        /// </summary>
        public Deck Deck { get; } = new();



        /// <summary>
        ///     Creates a new BlackJack game
        /// </summary>
        public BlackJackTable(
            DiscordSocketClient discordSocketClient,
            CurrencyService currencyService,
            InteractionHandlingService interactionHandlingService,
            IThreadChannel threadChannel,
            Guid gameId)
        {
            this.discordSocketClient = discordSocketClient ?? throw new ArgumentNullException(nameof(discordSocketClient));
            this.currencyService = currencyService ?? throw new ArgumentNullException(nameof(currencyService));
            this.interactionHandlingService = interactionHandlingService ?? throw new ArgumentNullException(nameof(interactionHandlingService));
            this.threadChannel = threadChannel ?? throw new ArgumentNullException(nameof(threadChannel));
            GameId = gameId;
        }



        /// <summary>
        ///     Add a new Player to the table
        /// </summary>
        /// <param name="userData"><see cref="EileenUserData"/></param>
        public void AddPlayer(EileenUserData userData)
        {
            if (IsGameActive)
            {
                Pending.Add(userData);
            }
            else
            {
                Players.Add(new BlackJackPlayer(userData));
                ThreadPool.QueueUserWorkItem(async (state) =>
                {
                    await RunGameLoop();
                });
            }
        }

        /// <summary>
        ///     Checks to see if someone is playing
        /// </summary>
        /// <param name="userData"><see cref="EileenUserData"/></param>
        /// <returns>boolean</returns>
        public bool IsPlaying(EileenUserData userData) => Players.Any(c => c.User.UserId == userData.UserId);

        /// <summary>
        ///     Removes a player from the game
        /// </summary>
        /// <param name="userData"><see cref="EileenUserData"/></param>
        public void RemovePlayer(EileenUserData userData)
        {
            Leaving.Add(userData);
        }

        /// <summary>
        /// Finds a Player that's seated at the table
        /// </summary>
        /// <param name="userData"></param>
        /// <returns></returns>
        public BlackJackPlayer GetPlayer(EileenUserData userData) => Players.FirstOrDefault(c => c.User.UserId == userData.UserId);

        /// <summary>
        /// Finds a Player that's seated at the table
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public BlackJackPlayer GetPlayer(ulong userId) => Players.FirstOrDefault(c => c.User.UserId == userId);




        /// <summary>
        ///     Invoked when a round starts
        /// </summary>
        public void HandleGameStart()
        {
            if (Pending.Any())
            {
                foreach (var pending in Pending)
                {
                    if (Players.Any(p => p.User.UserId == pending.UserId)) continue;
                    Players.Add(new(pending));
                }
                Pending.Clear();
            }
            IsGameActive = true;
        }

        /// <summary>
        ///     Invoked when a round finishes
        /// </summary>
        public void HandleGameFinish()
        {
            if (Leaving.Any())
            {
                foreach (var leaving in Leaving)
                {
                    var leaver = Players.FirstOrDefault(p => p.User.UserId == leaving.UserId);
                    if (leaver == null) continue;
                    Players.Remove(leaver);
                }
                Leaving.Clear();
            }
            foreach (var player in Players)
            {
                player.Hand.Clear();
            }
            Dealer.Hand.Clear();
            IsGameActive = false;
        }

        public void ResetGameLoop()
        {
            cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task RunGameLoop(bool reset = true)
        {
            if (isGameLoopRunning) return;
            if (reset) ResetGameLoop();
            isGameLoopRunning = true;

            var gameLoopId = Guid.NewGuid();
            var quickBetButtons = new ComponentBuilder()
                .WithButton("Bet 1", $"bet-1-{gameLoopId}")
                .WithButton("Bet 5", $"bet-5-{gameLoopId}")
                .WithButton("Bet 10", $"bet-10-{gameLoopId}");

            await threadChannel.SendMessageAsync("At least one Player has joined the table! The table will wait approximately 30 seconds for others to join in before starting the round. You may now place your bets with a quick button below or by typing in `/blackjack bet <amount>`",
                components: quickBetButtons.Build());

            RegisterQuickBetButtons(gameLoopId);

            await Task.Delay(TimeSpan.FromSeconds(15));
            await threadChannel.SendMessageAsync("15 seconds remaining before the round starts!");
            await Task.Delay(TimeSpan.FromSeconds(15));

            interactionHandlingService.RemoveButtonCallbacks($"bet-1-{gameLoopId}", $"bet-5-{gameLoopId}", $"bet-10-{gameLoopId}");

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                // add the pending and remove any that chose to leave
                HandleGameFinish();
                HandleGameStart();
                await EnsurePeopleHaveEnoughMoneyToPlay();

                if (Players.Count == 0)
                {
                    IsGameActive = false;
                    await threadChannel.SendMessageAsync("It seems that there are no Players this round. Whenever at least one Player re-joins the table, the Dealer will begin the initial countdown once again");
                    break;
                }
                DealOutHands();

                // Now then. We're going to iterate across the players and send them their hands
                // via a direct message because we don't have a way to send ephemeral messages. But
                // that's OK. We'll just DM the player their stuff and allow them to take hit/stand/splits
                // via ephemeral messages
                foreach (var player in Players)
                {
                    var user = await discordSocketClient.GetUserAsync(player.User.UserId);
                    var dm = await user.CreateDMChannelAsync();
                    await dm.SendFilesAsync(await player.GetHandAsAttachment(), $"Here is your current hand: {player.GetHandAsString()} for {player.Value}");
                }

                await ShowDealerHand();

                // Now then, player fun time
                foreach (var player in Players)
                {
                    await HandleCurrentPlayerHand(player, cancellationTokenSource.Token);
                }

                await Task.Delay(TimeSpan.FromSeconds(2));

                // Dealer time
                await HandleDealer();
                await HandleScoringCalculations();

                await threadChannel?.SendMessageAsync("The current round has concluded. There will be a 30 second delay before the start of the next round!");
                HandleGameFinish();
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            isGameLoopRunning = false;
        }

        public void EndGameLoop()
        {
            cancellationTokenSource.Cancel();
        }


        private async Task HandleScoringCalculations()
        {
            var winners = Players.Where(player => (player.Value > Dealer.Value && !player.IsBust) || player.IsBlackJack || (Dealer.IsBust && !player.IsBust)).ToList();
            var losers = Players.Where(player => (player.Value < Dealer.Value && !Dealer.IsBust) || (player.IsBust && !Dealer.IsBust)).ToList();
            var neutrals = Players.Where(player => (player.Value == Dealer.Value) || (player.IsBust && Dealer.IsBust));

            var users = new List<IUser>();
            foreach (var winner in winners
                .Union(losers)
                .Union(neutrals))
            {
                users.Add(await discordSocketClient.GetUserAsync(winner.User.UserId));
            }

            var roundResultBuilder = new StringBuilder();
            roundResultBuilder.AppendLine($"Winners: {string.Join(", ", from w in winners let wu = users.First(u => u.Id == w.User.UserId) select wu.Username)}");
            roundResultBuilder.AppendLine($"Losers: {string.Join(", ", from w in losers let wu = users.First(u => u.Id == w.User.UserId) select wu.Username)}");
            roundResultBuilder.AppendLine($"Neutrals: {string.Join(", ", from w in neutrals let wu = users.First(u => u.Id == w.User.UserId) select wu.Username)}");
            await threadChannel.SendMessageAsync(roundResultBuilder.ToString());

            var earningsBuilder = new StringBuilder();
            foreach (var winner in from w in winners
                                   let wu = users.First(u => u.Id == w.User.UserId)
                                   select (w, wu))
            {
                var winnerCurrency = currencyService.GetOrCreateCurrencyData(winner.w.User);
                var winnings = winner.w.IsBlackJack ? (ulong)Math.Round(winner.w.Bet * 1.5) : winner.w.Bet;
                earningsBuilder.AppendLine($"{winner.wu.Username}: {winnings}");
            }

            foreach (var loser in from l in losers
                                  let lu = users.First(u => u.Id == l.User.UserId)
                                  select (l, lu))
            {
                var loserCurrency = currencyService.GetOrCreateCurrencyData(loser.l.User);
                loserCurrency.Currency -= loser.l.Bet;
                earningsBuilder.AppendLine($"{loser.lu.Username}: -{loser.l.Bet}");
            }

            if (earningsBuilder.Length > 0)
            {
                await threadChannel.SendMessageAsync(earningsBuilder.ToString());
            }
        }

        private async Task EnsurePeopleHaveEnoughMoneyToPlay()
        {
            for (int i = Players.Count - 1; i >= 0; i--)
            {
                BlackJackPlayer player = Players[i];
                var playerCurrency = currencyService.GetOrCreateCurrencyData(player.User);
                if (playerCurrency.Currency < player.Bet || player.Bet == 0)
                {
                    await threadChannel.SendMessageAsync($"Unfortunately <@{player.User.UserId}> is stepping away from the table as they either did not set a bet in time OR they can't cover their own bet!");
                    Players.RemoveAt(i);
                }
            }
        }

        private void DealOutHands()
        {
            // Okay, time to play! Deal out everything to everyone.
            // We're going to deal out properly. So we'll do this twice
            for (var c = 0; c < 2; c++)
            {
                foreach (var player in Players)
                {
                    player.Hand.Add(Deck.GetNextCard());
                }
                Dealer.Hand.Add(Deck.GetNextCard());
            }
        }

        private async Task HandleCurrentPlayerHand(BlackJackPlayer currentPlayer, CancellationToken token)
        {
            // for a player hand, we need to show a message they can react to which will let them
            // 1. HIT
            // 2. STAND
            var hasStood = false;
            var uniqueId = Guid.NewGuid();
            var buttons = new ComponentBuilder()
                .WithButton("Hit", $"hit-{uniqueId}")
                .WithButton("Stand", $"stand-{uniqueId}");

            if (currentPlayer.IsSplittable)
            {
                //buttons = buttons
                //    .WithButton("Split", $"split-{uniqueId}");
            }

            interactionHandlingService.RegisterCallbackHandler($"hit-{uniqueId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (smc.User.Id != currentPlayer.User.UserId) return;
                var nextCard = Deck.GetNextCard();
                currentPlayer.Hand.Add(nextCard);
                await smc.RespondAsync($"<@{currentPlayer.User.UserId}> takes a hit: The {nextCard.GetDisplayName}");
                if (currentPlayer.IsBust)
                {
                    await threadChannel.SendFilesAsync(await currentPlayer.GetHandAsAttachment(), $"Busted! <@{currentPlayer.User.UserId}>'s hand. Showing: {currentPlayer.Value}");
                }
                else
                {
                    await threadChannel.SendFilesAsync(await currentPlayer.GetHandAsAttachment(), $"<@{currentPlayer.User.UserId}>'s hand. Showing: {currentPlayer.Value}", components: buttons.Build());
                }
            }));

            interactionHandlingService.RegisterCallbackHandler($"stand-{uniqueId}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (smc.User.Id != currentPlayer.User.UserId) return;
                await smc.RespondAsync($"<@{currentPlayer.User.UserId}> has finished their turn!", components: null);
                hasStood = true;
            }));

            if (currentPlayer.IsSplittable)
            {
                interactionHandlingService.RegisterCallbackHandler($"split-{uniqueId}", new InteractionButtonCallbackProvider(async smc =>
                {

                }));
            }
            

            var message = await threadChannel.SendFilesAsync(await currentPlayer.GetHandAsAttachment(), $"<@{currentPlayer.User.UserId}>'s turn! Showing: {currentPlayer.Value}", components: buttons.Build());
            SpinWait.SpinUntil(() => token.IsCancellationRequested || currentPlayer.IsBust || hasStood);
            interactionHandlingService.RemoveButtonCallbacks($"hit-{uniqueId}", $"stand-{uniqueId}", $"split-{uniqueId}");
        }

        private async Task HandleDealer()
        {
            await ShowDealerHand(false);
            await Task.Delay(TimeSpan.FromSeconds(2));
            while (Dealer.Value < 17)
            {
                var nextCard = Deck.GetNextCard();
                await threadChannel.SendMessageAsync($"The Dealer takes a hit! The {nextCard.GetDisplayName}!");
                Dealer.Hand.Add(nextCard);
                await ShowDealerHand(false);
                if (Dealer.IsBust)
                {
                    await threadChannel.SendMessageAsync("The Dealer has busted!");
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            if (!Dealer.IsBust)
            {
                await threadChannel.SendMessageAsync($"The Dealer has finished their turn and ended their turn with a hand score of {Dealer.Value}!");
            }
        }

        private async Task<IUserMessage> ShowDealerHand(bool hideFirstCard = true)
        {
            return await threadChannel.SendFilesAsync(await Dealer.GetHandAsAttachment(hideFirstCard), $"Dealer's is showing {(hideFirstCard ? Dealer.DealerValue : Dealer.Value)} total.");
        }

        private void RegisterQuickBetButtons(Guid gameLoopGuid)
        {
            static async Task HandleBet(SocketMessageComponent smc, BlackJackPlayer player, ulong amount)
            {
                if (player is null) return;
                player.Bet = amount;
                await smc.RespondAsync($"Your bet has been set to {amount}");
            }

            interactionHandlingService.RegisterCallbackHandler($"bet-1-{gameLoopGuid}", new InteractionButtonCallbackProvider(async smc =>
            {
                await HandleBet(smc, GetPlayer(smc.User.Id), 1);
            }));

            interactionHandlingService.RegisterCallbackHandler($"bet-5-{gameLoopGuid}", new InteractionButtonCallbackProvider(async smc =>
            {
                await HandleBet(smc, GetPlayer(smc.User.Id), 5);
            }));

            interactionHandlingService.RegisterCallbackHandler($"bet-10-{gameLoopGuid}", new InteractionButtonCallbackProvider(async smc =>
            {
                await HandleBet(smc, GetPlayer(smc.User.Id), 10);
            }));

        }

    }
    */
}
