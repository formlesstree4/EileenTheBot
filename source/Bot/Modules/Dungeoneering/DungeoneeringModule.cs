using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bot.Models;
using Bot.Preconditions;
using Bot.Services;
using Bot.Services.Dungeoneering;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Bot.Modules.Dungeoneering
{

    [Group("dungeoneer")]
    public sealed class DungeoneeringModule : ModuleBase<SocketCommandContext>
    {

        public DungeoneeringMainService DungeoneeringService { get; set; }

        public BetterPaginationService PaginationService { get; set; }

        public UserService UserService { get; set; }

        public DiscordSocketClient Client { get; set; }

        public Random Rng { get; set; }



        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [Command("help")]
        public async Task HelpCommandAsync()
        {
            await HandleHelpAsync();
        }
        
        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [Command("register")]
        public async Task RegisterCommandAsync()
        {
            await HandleRegistrationAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [Command("fight")]
        public async Task FightCommandAsync()
        {
            await HandleFightAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [Command("attack")]
        public async Task AttackCommandAsync()
        {
            await HandleAttackAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [Command("run")]
        public async Task RunAwayCommandAsync()
        {
            await HandleFleeAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [Command("status")]
        public async Task StatusCommandAsync()
        {
            await HandleStatusAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [Command("assist")]
        public async Task AssistCommandAsync()
        {
            await Context.Channel.SendMessageAsync("Assisting is not supported yet!");
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [Command("deter")]
        public async Task DeterCommandAsync()
        {
            await Context.Channel.SendMessageAsync("Deterring is not supported yet!");
        }
        


        private async Task HandleHelpAsync()
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("HELP - This command right here");
            messageBuilder.AppendLine("REGISTER - Enrolls the User in the Dungeoneering System");
            messageBuilder.AppendLine("FIGHT - Initiates an encounter in the current Channel. One fight per-channel!");
            messageBuilder.AppendLine("ATTACK - When in an encounter, the Player will attack the Monster");
            messageBuilder.AppendLine("RUN - When in an encounter, the Player will flee! This has a 1/5 chance of failing");
            await Context.Channel.SendMessageAsync(messageBuilder.ToString());
        }

        private async Task HandleRegistrationAsync()
        {
            if (await DungeoneeringService.IsUserRegisteredAsync(Context.User))
            {
                await Context.Channel.SendMessageAsync("You are already registered for Dungeoneering! You can view your Player Card in your profile!");
                return;
            }
            await DungeoneeringService.RegisterPlayerAsync(Context.User);
            await Context.Channel.SendMessageAsync("Congratulations and welcome to Dungeoneering! Your guild card has been created and is now part of your Profile!");
            await Context.Channel.SendMessageAsync("To know more about what you can do with Dungeoneering, just type in `dungeoneer help`");
            await Context.Channel.SendMessageAsync("All the commands will be printed so you can see what all is now accessible.");
            var profileCallback = new ProfileCallback(await UserService.GetOrCreateUserData(Context.User), Context.User, new Discord.EmbedBuilder());
            var builder = await DungeoneeringService.CreateDungeoneeringProfilePage(profileCallback);
            await Context.Channel.SendMessageAsync(embed: builder.PageBuilder.Build());
        }

        private async Task HandleFightAsync()
        {
            var encounter = await DungeoneeringService.GetEncounterAsync(Context.Channel);
            if (encounter != null)
            {
                await Context.Channel.SendMessageAsync("This channel already has an encounter going! Finish that first, THEN you can start another one!");
                return;
            }
            if (await DungeoneeringService.IsUserInAnyEncounterAsync(Context.User))
            {
                await Context.Channel.SendMessageAsync("Finish your previous encounter! One at a time!");
                return;
            }
            encounter = await DungeoneeringService.CreateEncounterAsync(Context.User, Context.Channel);

            var messageBuilder = new StringBuilder();
            await Context.Channel.SendMessageAsync($"{Context.User.Mention} has started an encounter!");
            await Context.Channel.SendMessageAsync($"In front of {Context.User.Mention} is a Level {encounter.ActiveMonster.GetActualPower()} '{encounter.ActiveMonster.Name}'.");
            if (encounter.ActiveMonster.Equipment.Any())
            {
                await Context.Channel.SendMessageAsync($"The '{encounter.ActiveMonster.Name}' seems to be wearing some eqipment as well. Be careful!");
            }
            await Context.Channel.SendMessageAsync($"The encounter will last until {Context.User.Mention} defeats the Monster or flees!");
        }

        private async Task HandleAttackAsync()
        {
            var encounter = await DungeoneeringService.GetEncounterAsync(Context.Channel);
            var playerCard = await DungeoneeringService.GetPlayerCardAsync(Context.User);
            if (encounter == null) return;
            if (encounter.PlayerId != Context.User.Id) return;
            if (encounter.ActiveMonster.GetActualPower() < playerCard.AttackPower)
            {
                await Context.Channel.SendMessageAsync($"{Context.User.Mention} has successfully defeated the {encounter.ActiveMonster.Name}!");
                await DungeoneeringService.HandleVictoryAsync(playerCard, encounter);
            }
            else
            {
                await Context.Channel.SendMessageAsync($"{Context.User.Mention} was brutally killed by the {encounter.ActiveMonster.Name} and has respawned back at the 'Guild Hall'. Your Attack Power has been decreased!");
                await DungeoneeringService.HandleDefeatAsync(playerCard, encounter);
            }
        }

        private async Task HandleFleeAsync()
        {
            var encounter = await DungeoneeringService.GetEncounterAsync(Context.Channel);
            var playerCard = await DungeoneeringService.GetPlayerCardAsync(Context.User);
            if (encounter == null) return;
            if (encounter.PlayerId != Context.User.Id) return;
            var fleeChance = Rng.Next(100);
            if (fleeChance >= 80)
            {
                await Context.Channel.SendMessageAsync($"{Context.User.Mention} was unable to flee, was killed by the {encounter.ActiveMonster.Name}, and has respawned back at the 'Guild Hall'. Your Attack Power has been decreased!");
                await DungeoneeringService.HandleDefeatAsync(playerCard, encounter);
            }
            else
            {
                await Context.Channel.SendMessageAsync($"{Context.User.Mention} has fled successfully!");
                await DungeoneeringService.HandleFleeAsync(playerCard, encounter);
            }

        }

        private async Task HandleStatusAsync()
        {
            var encounter = await DungeoneeringService.GetEncounterAsync(Context.Channel);
            if (encounter == null)
            {
                await Context.Channel.SendMessageAsync("There is no encounter at this time!");
                return;
            }
            var playerCard = await DungeoneeringService.GetPlayerCardAsync(encounter.PlayerId);
            var userDetails = await (Client as IDiscordClient).GetUserAsync(encounter.PlayerId);
            await Context.Channel.SendMessageAsync($"{userDetails.Mention} is fighting '{encounter.ActiveMonster.Name}' with a power of {encounter.ActiveMonster.GetActualPower()}");
            await Context.Channel.SendMessageAsync($"{userDetails.Mention} has a power of {playerCard.GetActualPower()}.");
        }

    }

}