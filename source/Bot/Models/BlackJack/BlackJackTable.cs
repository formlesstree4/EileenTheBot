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
            this.discordSocketClient = discordSocketClient;
            this.currencyService = currencyService;
            this.interactionHandlingService = interactionHandlingService;
            this.threadChannel = threadChannel;
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
        ///     A check to see if the players have finished
        /// </summary>
        /// <returns></returns>
        public bool HavePlayersFinished { get; private set; } = false;



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

            await threadChannel.SendMessageAsync("At least one Player has joined the table! The table will wait approximately 30 seconds for others to join in before starting the round. This is a one time courtesy and future rounds will start much faster.");
            await Task.Delay(TimeSpan.FromSeconds(15));
            await threadChannel.SendMessageAsync("15 seconds remaining before the round starts!");
            await Task.Delay(TimeSpan.FromSeconds(15));
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
            while (Dealer.Value < 17)
            {
                var nextCard = Deck.GetNextCard();
                await threadChannel.SendMessageAsync($"The Dealer takes a hit! The {nextCard.GetDisplayName}!");
                Dealer.Hand.Add(nextCard);
                await Task.Delay(TimeSpan.FromSeconds(2));
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
    }

}
