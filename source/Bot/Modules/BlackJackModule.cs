using Bot.Services;
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



    }
}
