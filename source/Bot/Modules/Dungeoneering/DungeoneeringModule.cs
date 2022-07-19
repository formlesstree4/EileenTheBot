using Bot.Models;
using Bot.Models.Dungeoneering.Special.Equipment;
using Bot.Preconditions;
using Bot.Services;
using Bot.Services.Dungeoneering;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Modules.Dungeoneering
{

    [Group("dungeoneer", "Interact with the Dungeoneer system")]
    public sealed class DungeoneeringModule : InteractionModuleBase
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
        [SlashCommand("help", "Get help over Dungeoneer")]
        public async Task HelpCommandAsync()
        {
            await HandleHelpAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [SlashCommand("register", "Register yourself as part of Dungeoneer")]
        public async Task RegisterCommandAsync()
        {
            await HandleRegistrationAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [SlashCommand("fight", "Initiate a fight!")]
        public async Task FightCommandAsync()
        {
            await HandleFightAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [SlashCommand("attack", "Declares an attack!")]
        public async Task AttackCommandAsync()
        {
            await HandleAttackAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [SlashCommand("run", "Flee the current, active battle")]
        public async Task RunAwayCommandAsync()
        {
            await HandleFleeAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [SlashCommand("status", "Check your current details")]
        public async Task StatusCommandAsync()
        {
            await HandleStatusAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [SlashCommand("assist", "Assist the current player's battle")]
        public async Task AssistCommandAsync()
        {
            await HandleAssistingAsync();
        }

        [UseErectorPermissions(false, true)]
        [RequireContext(ContextType.Guild)]
        [SlashCommand("deter", "Hinder the current player's battle")]
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
            await RespondAsync(messageBuilder.ToString(), ephemeral: true);
        }

        private async Task HandleRegistrationAsync()
        {
            if (await dungeoneeringMainService.IsUserRegisteredAsync(Context.User))
            {
                await RespondAsync("You are already registered for Dungeoneering! You can view your Player Card in your profile!");
                return;
            }
            await dungeoneeringMainService.RegisterPlayerAsync(Context.User);
            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine("Congratulations and welcome to Dungeoneering! Your guild card has been created and is now part of your Profile!");
            responseBuilder.AppendLine("To know more about what you can do with Dungeoneering, look at the help command");
            responseBuilder.AppendLine("All the commands will be printed so you can see what all is now accessible.");
            var profileCallback = new ProfileCallback(await userService.GetOrCreateUserData(Context.User), Context.User, new EmbedBuilder());
            var builder = await dungeoneeringMainService.CreateDungeoneeringProfilePage(profileCallback);
            await RespondAsync(responseBuilder.ToString(), embed: builder.PageBuilder.Build());
        }

        private async Task HandleFightAsync()
        {
            var encounter = await dungeoneeringMainService.GetEncounterAsync(Context.Channel);
            if (encounter != null)
            {
                await RespondAsync("This channel already has an encounter going! Finish that first, THEN you can start another one!", ephemeral: true);
                return;
            }
            if (await dungeoneeringMainService.IsUserInAnyEncounterAsync(Context.User))
            {
                await RespondAsync("Finish your previous encounter! One at a time!", ephemeral: true);
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
            await RespondAsync(messageBuilder.ToString());
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
                await RespondAsync($"{Context.User.Username} has successfully defeated the {encounter.ActiveMonster.Name}!");
                await dungeoneeringMainService.HandleVictoryAsync(playerCard, encounter);
            }
            else
            {
                await RespondAsync($"{Context.User.Username} was brutally killed by the {encounter.ActiveMonster.Name} and has respawned back at the 'Guild Hall'. {Context.User.Username}, your Attack Power has been decreased by one!");
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
                await RespondAsync($"{Context.User.Username} was unable to flee, was killed by the {encounter.ActiveMonster.Name}, and has respawned back at the 'Guild Hall'. {Context.User.Username}, Attack Power has been decreased!");
                await dungeoneeringMainService.HandleDefeatAsync(playerCard, encounter);
            }
            else
            {
                await RespondAsync($"{Context.User.Username} has fled successfully!");
                await dungeoneeringMainService.HandleFleeAsync(playerCard, encounter);
            }

        }

        private async Task HandleStatusAsync()
        {
            var encounter = await dungeoneeringMainService.GetEncounterAsync(Context.Channel);
            if (encounter == null)
            {
                await RespondAsync("There is no encounter at this time in this channel!", ephemeral: true);
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
            await RespondAsync(responseBuilder.ToString(), ephemeral: true);
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
            await RespondAsync($"{Context.User.Username} has decided to assist '{encounterPlayer.Username}' by boosting their Attack Power by +{playerCard.GetActualPower()}!");
        }

        private async Task HandleDeterringAsync()
        {
            var encounter = await dungeoneeringMainService.GetEncounterAsync(Context.Channel);
            var playerCard = await dungeoneeringMainService.GetPlayerCardAsync(Context.User);
            if (encounter == null) return;
            if (encounter.PlayerId == Context.User.Id) return;
            encounter.Assistants.Remove(Context.User.Id);
            encounter.Instigators.Add(Context.User.Id);
            await RespondAsync($"{Context.User.Username} has decided to assist the '{encounter.ActiveMonster.Name}' by boosting their Attack Power by +{playerCard.GetActualPower()}!");
        }


        [Group("query", "look up information about Dungeoneering!")]
        public sealed class DungeoneeringResearchModule : InteractionModuleBase
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
            [SlashCommand("help", "Get help about the query system")]
            public async Task QueryInfoAsync()
            {
                await RespondAsync("You can query using the `weapon` and `armor` as sub-text searches. For example: `/dungeoneer query weapon Sword` will return all weapons of the name 'Sword'");
            }

            [UseErectorPermissions(false, true)]
            [RequireContext(ContextType.Guild)]
            [SlashCommand("weapon", "Lookup a weapon")]
            public async Task LookupWeaponAsync([Summary("name", "The name of a weapon"), Autocomplete(typeof(WeaponAutocompleteHandler))] int id)
            {
                var weapons = equipmentService.Weapons.Where(c => c.EquipmentLevel == id);

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

                await paginationService.Send(Context, Context.Channel,
                    new BetterPaginationMessage(embeds, false, Context.User));

            }

            [UseErectorPermissions(false, true)]
            [RequireContext(ContextType.Guild)]
            [SlashCommand("armor", "Lookup an armor piece")]
            public async Task LookupArmorAsync([Summary("name", "The name of the armor"), Autocomplete(typeof(ArmorAutocompleteHandler))] int id)
            {
                var armor = equipmentService.Armor.Where(c => c.EquipmentId == id);

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

                await paginationService.Send(Context, Context.Channel,
                    new BetterPaginationMessage(embeds, false, Context.User));

            }


        }


        private sealed class WeaponAutocompleteHandler : AutocompleteHandler
        {
            public override Task<AutocompletionResult> GenerateSuggestionsAsync(
                IInteractionContext context,
                IAutocompleteInteraction autocompleteInteraction,
                IParameterInfo parameter,
                IServiceProvider services)
            {
                var weaponService = services.GetRequiredService<EquipmentService>();
                var text = autocompleteInteraction.Data.Current.Value.ToString();
                var itemsToReturn = weaponService.Weapons
                    .Where(c => c.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .Select(e => new AutocompleteResult($"Lv {e.EquipmentLevel}: {e.Name}", e.EquipmentId));
                return Task.FromResult(AutocompletionResult.FromSuccess(itemsToReturn));
            }
        }

        private sealed class ArmorAutocompleteHandler : AutocompleteHandler
        {
            public override Task<AutocompletionResult> GenerateSuggestionsAsync(
                IInteractionContext context,
                IAutocompleteInteraction autocompleteInteraction,
                IParameterInfo parameter,
                IServiceProvider services)
            {
                var weaponService = services.GetRequiredService<EquipmentService>();
                var text = autocompleteInteraction.Data.Current.Value.ToString();
                var itemsToReturn = weaponService.Armor
                    .Where(c => c.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .Select(e => new AutocompleteResult($"Lv {e.EquipmentLevel}: {e.Name}", e.EquipmentId));
                return Task.FromResult(AutocompletionResult.FromSuccess(itemsToReturn));
            }
        }


    }

}
