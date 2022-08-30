using Bot.Services;
using Discord;
using Discord.Interactions;
using System.Threading.Tasks;

namespace Bot.Modules
{
    [Group("blackjack", "Groups all BlackJack commands together")]
    public sealed class BlackJackModule : InteractionModuleBase
    {
        private readonly BlackJackService blackJackService;
        private readonly CurrencyService currencyService;
        private readonly InteractionHandlingService interactionHandlingService;
        private readonly UserService userService;

        public BlackJackModule(
            BlackJackService blackJackService,
            CurrencyService currencyService,
            InteractionHandlingService interactionHandlingService,
            UserService userService)
        {
            this.blackJackService = blackJackService ?? throw new System.ArgumentNullException(nameof(blackJackService));
            this.currencyService = currencyService ?? throw new System.ArgumentNullException(nameof(currencyService));
            this.interactionHandlingService = interactionHandlingService ?? throw new System.ArgumentNullException(nameof(interactionHandlingService));
            this.userService = userService ?? throw new System.ArgumentNullException(nameof(userService));
        }

        [SlashCommand("begin", "Opens a new BlackJack Table")]
        public async Task CreateTable()
        {
            var table = await blackJackService.CreateNewBlackJackGame(Context.Guild);
            await RespondAsync("A new game table has been created!");
        }


        [SlashCommand("bet", "Gets or sets your static Bet for the room")]
        public async Task Bet(ulong? amount = null)
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            var currencyData = currencyService.GetOrCreateCurrencyData(userData);
            if (Context.Channel is not IThreadChannel tc)
            {
                await RespondAsync("You can only do this Command in an appropriate Table Thread", ephemeral: true);
                return;
            }
            var gameData = blackJackService.FindBlackJackGame(Context.Guild, tc);
            if (!gameData.IsPlaying(userData))
            {
                await RespondAsync("You can only do this Command in this room if you're playing!", ephemeral: true);
                return;
            }
            var playerData = gameData.GetPlayer(userData);

            if (amount is null)
            {
                await RespondAsync($"Your current bet: {playerData.Bet}", ephemeral: true);
            }
            else
            {
                if (gameData.IsGameActive)
                {
                    await RespondAsync("You cannot alter your bet while the game is running. Please wait until after the round is over", ephemeral: true);
                    return;
                }
                else
                {
                    if (currencyData.Currency >= amount)
                    {
                        playerData.Bet = (ulong)amount;
                        await RespondAsync($"You have set your Bet to {amount}", ephemeral: true);
                    }
                }

            }


        }


    }
}
