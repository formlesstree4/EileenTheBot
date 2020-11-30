using System.Threading.Tasks;
using Bot.Preconditions;
using Bot.Services;
using Bot.Services.Dungeoneering;
using Discord.Commands;

namespace Bot.Modules.Dungeoneering
{

    public sealed class DungeoneeringModule : ModuleBase<SocketCommandContext>
    {

        public DungeoneeringMainService DungeoneeringService { get; set; }

        public BetterPaginationService PaginationService { get; set; }


        [UseErectorPermissions(false, true)]
        [Command("dungeoneer")]
        [Summary("The entrypoint into everything relating to Dungeoneering! You can use the 'help' command (dungeoneer help) for all the details")]
        public async Task DungeoneerCommandBroker(
            [Summary("The actual command you want to execute for Dungeoneer")]string command,
            [Remainder, Summary("A collection of parameters that are to be passed along for use with the given command")] string[] parameters = null)
        {

            switch(command.ToUpperInvariant())
            {
                case "HELP":
                    await HandleHelp();
                    break;
                case "REGISTER":
                    await HandleRegistration();
                    break;
                case "FIGHT":
                    break;
            }
        }

        private async Task HandleHelp()
        {
            await Context.Channel.SendMessageAsync("The help pages are pending!");
        }

        private async Task HandleRegistration()
        {
            if (await DungeoneeringService.IsUserRegistered(Context.User.Id))
            {
                await Context.Channel.SendMessageAsync("You are already registered for Dungeoneering! You can view your Player Card in your profile!");
            }
            
        }

    }

}