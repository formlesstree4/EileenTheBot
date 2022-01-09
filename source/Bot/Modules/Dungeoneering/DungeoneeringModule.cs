using Bot.Models;
using Bot.Preconditions;
using Bot.Services;
using Bot.Services.Dungeoneering;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Modules.Dungeoneering
{

    [Group("dungeoneer"), Alias("d")]
    public sealed class DungeoneeringModule : ModuleBase<SocketCommandContext>
    {
        private readonly DungeoneeringMainService dungeoneeringMainService;
        private readonly UserService userService;
        private readonly DiscordSocketClient client;
        private readonly Random rng;


        public DungeoneeringModule(
            DungeoneeringMainService dungeoneeringMainService,
            UserService userService,
            DiscordSocketClient client,
            Random rng)
        {
            this.dungeoneeringMainService = dungeoneeringMainService ?? throw new ArgumentNullException(nameof(dungeoneeringMainService));
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }



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
            await HandleAssistingAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [Command("deter")]
        public async Task DeterCommandAsync()
        {
            await HandleDeterringAsync();
            // await Context.Channel.SendMessageAsync("Deterring is not supported yet!");
        }



        private async Task HandleHelpAsync()
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("HELP - This command right here");
            messageBuilder.AppendLine("REGISTER - Enrolls the User in the Dungeoneering System");
            messageBuilder.AppendLine("FIGHT - Initiates an encounter in the current Channel. One fight per-channel!");
            messageBuilder.AppendLine("ATTACK - When in an encounter, the Player will attack the Monster");
            messageBuilder.AppendLine("RUN - When in an encounter, the Player will flee! This has a 1/5 chance of failing");
            await ReplyAsync(messageBuilder.ToString());
        }

        private async Task HandleRegistrationAsync()
        {
            if (await dungeoneeringMainService.IsUserRegisteredAsync(Context.User))
            {
                await ReplyAsync("You are already registered for Dungeoneering! You can view your Player Card in your profile!");
                return;
            }
            await dungeoneeringMainService.RegisterPlayerAsync(Context.User);
            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine("Congratulations and welcome to Dungeoneering! Your guild card has been created and is now part of your Profile!");
            responseBuilder.AppendLine("To know more about what you can do with Dungeoneering, just type in `dungeoneer help`");
            responseBuilder.AppendLine("All the commands will be printed so you can see what all is now accessible.");
            var profileCallback = new ProfileCallback(await userService.GetOrCreateUserData(Context.User), Context.User, new EmbedBuilder());
            var builder = await dungeoneeringMainService.CreateDungeoneeringProfilePage(profileCallback);
            await ReplyAsync(responseBuilder.ToString());
            await ReplyAsync(embed: builder.PageBuilder.Build());
        }

        private async Task HandleFightAsync()
        {
            var encounter = await dungeoneeringMainService.GetEncounterAsync(Context.Channel);
            if (encounter != null)
            {
                await ReplyAsync("This channel already has an encounter going! Finish that first, THEN you can start another one!");
                return;
            }
            if (await dungeoneeringMainService.IsUserInAnyEncounterAsync(Context.User))
            {
                await ReplyAsync("Finish your previous encounter! One at a time!");
                return;
            }
            encounter = await dungeoneeringMainService.CreateEncounterAsync(Context.User, Context.Channel);

            var playerCard = await dungeoneeringMainService.GetPlayerCardAsync(Context.User);
            var playerPower = playerCard.GetActualPower();

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"{Context.User.Username} has started an encounter!");
            messageBuilder.AppendLine($"In front of {Context.User.Username} (Attack Power: {playerPower}) is a Level {encounter.ActiveMonster.MonsterLevel} '{encounter.ActiveMonster.Name}'.");
            if (encounter.ActiveMonster.Equipment.Any())
            {
                messageBuilder.AppendLine($"The '{encounter.ActiveMonster.Name}' seems to be wearing some equipment as well. Be careful and perhaps use the `status` command to check!!");
            }
            messageBuilder.AppendLine($"The encounter will last until {Context.User.Username} defeats the Monster or flees!");
            await ReplyAsync(messageBuilder.ToString());
        }

        private async Task HandleAttackAsync()
        {
            var encounter = await dungeoneeringMainService.GetEncounterAsync(Context.Channel);
            var playerCard = await dungeoneeringMainService.GetPlayerCardAsync(Context.User);
            if (encounter == null) return;
            if (encounter.PlayerId != Context.User.Id) return;

            var playerPower = playerCard.GetActualPower();
            var monsterPower = encounter.ActiveMonster.GetActualPower();

            var playerBoost = 0;
            var monsterBoost = 0;

            if (encounter.Assistants.Any())
            {
                var assistants = await dungeoneeringMainService.GetPlayerCardsAsync(encounter.Assistants);
                playerBoost += assistants.Sum(c => c.GetActualPower());
                playerPower += playerBoost;
            }

            if (encounter.Instigators.Any())
            {
                var instigators = await dungeoneeringMainService.GetPlayerCardsAsync(encounter.Instigators);
                monsterBoost += instigators.Sum(c => c.GetActualPower());
                monsterPower += monsterBoost;
            }

            if (monsterPower < playerPower)
            {
                await ReplyAsync($"{Context.User.Username} has successfully defeated the {encounter.ActiveMonster.Name}!");
                await dungeoneeringMainService.HandleVictoryAsync(playerCard, encounter);
            }
            else
            {
                await ReplyAsync($"{Context.User.Username} was brutally killed by the {encounter.ActiveMonster.Name} and has respawned back at the 'Guild Hall'. Your Attack Power has been decreased!");
                await dungeoneeringMainService.HandleDefeatAsync(playerCard, encounter);
            }
        }

        private async Task HandleFleeAsync()
        {
            var encounter = await dungeoneeringMainService.GetEncounterAsync(Context.Channel);
            var playerCard = await dungeoneeringMainService.GetPlayerCardAsync(Context.User);
            if (encounter == null) return;
            if (encounter.PlayerId != Context.User.Id) return;
            var fleeChance = rng.Next(100);
            if (fleeChance >= 80)
            {
                await ReplyAsync($"{Context.User.Username} was unable to flee, was killed by the {encounter.ActiveMonster.Name}, and has respawned back at the 'Guild Hall'. Your Attack Power has been decreased!");
                await dungeoneeringMainService.HandleDefeatAsync(playerCard, encounter);
            }
            else
            {
                await ReplyAsync($"{Context.User.Username} has fled successfully!");
                await dungeoneeringMainService.HandleFleeAsync(playerCard, encounter);
            }

        }

        private async Task HandleStatusAsync()
        {
            var encounter = await dungeoneeringMainService.GetEncounterAsync(Context.Channel);
            if (encounter == null)
            {
                await ReplyAsync("There is no encounter at this time!");
                return;
            }
            var playerCard = await dungeoneeringMainService.GetPlayerCardAsync(encounter.PlayerId);
            var userDetails = await (client as IDiscordClient).GetUserAsync(encounter.PlayerId);
            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine($"{userDetails.Username} is fighting '{encounter.ActiveMonster.Name}'.");

            var playerPower = playerCard.GetActualPower();
            var monsterPower = encounter.ActiveMonster.GetActualPower();

            var playerBoost = 0;
            var monsterBoost = 0;

            if (encounter.Assistants.Any())
            {
                var assistants = await dungeoneeringMainService.GetPlayerCardsAsync(encounter.Assistants);
                playerBoost += assistants.Sum(c => c.GetActualPower());
            }

            if (encounter.Instigators.Any())
            {
                var instigators = await dungeoneeringMainService.GetPlayerCardsAsync(encounter.Instigators);
                monsterBoost += instigators.Sum(c => c.GetActualPower());
            }

            responseBuilder.AppendLine($"{userDetails.Username} has a Power of {playerCard.GetActualPower()} + {playerBoost} (from {encounter.Assistants.Count} helpers)");
            responseBuilder.AppendLine($"{encounter.ActiveMonster.Name} has a Power of {encounter.ActiveMonster.GetActualPower()} + {monsterBoost} (from {encounter.Instigators.Count} helpers)");
            await ReplyAsync(responseBuilder.ToString());
        }

        private async Task HandleAssistingAsync()
        {
            var encounter = await dungeoneeringMainService.GetEncounterAsync(Context.Channel);
            var playerCard = await dungeoneeringMainService.GetPlayerCardAsync(Context.User);
            var encounterPlayer = await ((IDiscordClient)client).GetUserAsync(encounter.PlayerId);

            if (encounter == null) return;
            if (encounter.PlayerId == Context.User.Id) return;
            encounter.Assistants.Add(Context.User.Id);
            encounter.Instigators.Remove(Context.User.Id);
            await ReplyAsync($"{Context.User.Username} has decided to assist {encounterPlayer.Username} by boosting their Attack Power by +{playerCard.GetActualPower()}!");
        }

        private async Task HandleDeterringAsync()
        {
            var encounter = await dungeoneeringMainService.GetEncounterAsync(Context.Channel);
            var playerCard = await dungeoneeringMainService.GetPlayerCardAsync(Context.User);
            if (encounter == null) return;
            if (encounter.PlayerId == Context.User.Id) return;
            encounter.Assistants.Remove(Context.User.Id);
            encounter.Instigators.Add(Context.User.Id);
            await ReplyAsync($"{Context.User.Username} has decided to assist the '{encounter.ActiveMonster.Name}' by boosting their Attack Power by +{playerCard.GetActualPower()}!");
        }


        [Group("query")]
        public sealed class DungeoneeringResearchModule : ModuleBase<SocketCommandContext>
        {
            private readonly EquipmentService equipmentService;
            private readonly BetterPaginationService paginationService;

            public DungeoneeringResearchModule(
                EquipmentService equipmentService,
                BetterPaginationService paginationService)
            {
                this.equipmentService = equipmentService ?? throw new ArgumentNullException(nameof(equipmentService));
                this.paginationService = paginationService ?? throw new ArgumentNullException(nameof(paginationService));
            }


            [UseErectorPermissions(false, true)]
            [RequireContext(ContextType.Guild)]
            [Command]
            public async Task QueryInfoAsync()
            {
                await ReplyAsync("You can query using the `weapon` and `armor` as sub-text searches. For example: `dungeoneer query weapon Sword` will return all weapons of the name 'Sword'");
            }

            [UseErectorPermissions(false, true)]
            [RequireContext(ContextType.Guild)]
            [Command("weapon")]
            public async Task LookupWeaponAsync([Remainder] string name)
            {
                var weapons = equipmentService.Weapons
                    .Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.EquipmentLevel);

                var embeds = new List<Embed>();

                foreach(var weapon in weapons)
                {
                    var w = weapon.ToEquipment();
                    embeds.Add(
                        new EmbedBuilder()
                            .AddField("Power", w.AttackPower, true)
                            .AddField("Value", w.Price, true)
                            .AddField("Location", w.Location, true)
                            .WithTitle(weapon.Name)
                            .WithCurrentTimestamp()
                            .Build());
                }

                await paginationService.Send(
                    Context.Channel,
                    new BetterPaginationMessage(embeds, false, Context.User));

            }

            [UseErectorPermissions(false, true)]
            [RequireContext(ContextType.Guild)]
            [Command("armor")]
            public async Task LookupArmorAsync([Remainder] string name)
            {
                var armor = equipmentService.Armor
                    .Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.EquipmentLevel);

                var embeds = new List<Embed>();

                foreach(var armorPiece in armor)
                {
                    var w = armorPiece.ToEquipment();
                    embeds.Add(
                        new EmbedBuilder()
                            .AddField("Power", w.AttackPower, true)
                            .AddField("Value", w.Price, true)
                            .AddField("Location", w.Location, true)
                            .WithTitle(armorPiece.Name)
                            .WithCurrentTimestamp()
                            .Build());
                }

                await paginationService.Send(
                    Context.Channel,
                    new BetterPaginationMessage(embeds, false, Context.User));

            }


        }

    }

}
