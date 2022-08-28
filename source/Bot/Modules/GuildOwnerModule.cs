using Bot.Models.ChannelCommunication;
using Bot.Services;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Modules
{

    [Group("guild", "Guild Owner specific commands for managing and configuring the Guild"), RequireUserPermission(GuildPermission.ManageGuild), RequireContext(ContextType.Guild)]
    public sealed class GuildOwnerModule : InteractionModuleBase
    {
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly ChannelCommunicationService channelCommunicationService;

        public GuildOwnerModule(
            ServerConfigurationService serverConfigurationService,
            ChannelCommunicationService channelCommunicationService)
        {
            this.serverConfigurationService = serverConfigurationService ?? throw new ArgumentNullException(nameof(serverConfigurationService));
            this.channelCommunicationService = channelCommunicationService;
        }

        [SlashCommand("prefix", "Alters the character prefix the bot will use in order to be triggered")]
        public async Task SetPrefixAsync([Summary("prefix", "The new prefix to use")] char prefix)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(Context.Guild);
            configuration.CommandPrefix = prefix;
            await RespondAsync($"This server now uses the character prefix '{prefix}'", ephemeral: true);
        }


        [Group("permissions", "Manages permissions for slash commands across channels"), RequireUserPermission(GuildPermission.ManageChannels)]
        public sealed class PermissionsModule : InteractionModuleBase
        {
            private readonly CommandPermissionsService commandPermissionsService;
            private readonly InteractionService interactionService;

            public PermissionsModule(
                CommandPermissionsService commandPermissionsService,
                InteractionService interactionService)
            {
                this.commandPermissionsService = commandPermissionsService ?? throw new ArgumentNullException(nameof(commandPermissionsService));
                this.interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
            }



            [SlashCommand("allow", "Enables a command for a particular room.")]
            public async Task HandleAllowCommand(
                [Summary("channel", "The channel to explicitly enable a command for")] ITextChannel channel,
                [Summary("command", "The command to allow"), Autocomplete(typeof(IntegratedCommandsAutocompleteHandler))] string command)
            {
                var permissions = await commandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
                var discordCommand = interactionService.SlashCommands.FirstOrDefault(c => c.Name.Equals(command, StringComparison.OrdinalIgnoreCase));
                if (discordCommand is null)
                {
                    await RespondAsync($"The command '{command}' does not exist in the Bot");
                    return;
                }
                var commandPermissions = permissions.GetOrAddCommand(discordCommand);
                if (commandPermissions is null)
                {
                    await RespondAsync($"The command '{command}' does not integrate into the Permissions System");
                    return;
                }
                if (!commandPermissions.Channels.IsChannelAllowed(Context.Channel))
                {
                    commandPermissions.Channels.Allowed.Add(channel.Id);
                }
                if (commandPermissions.Channels.IsChannelBlocked(channel))
                {
                    commandPermissions.Channels.Blocked.Remove(channel.Id);
                }
                await RespondAsync($"The command '{command}' has been enabled for <#{channel.Id}>");
            }

            [SlashCommand("deny", "Disables a command for a particular room.")]
            public async Task HandleDenyCommand(
                [Summary("channel", "The channel to explicitly disable a command for")] ITextChannel channel,
                [Summary("command", "The command to disable"), Autocomplete(typeof(IntegratedCommandsAutocompleteHandler))] string command)
            {
                var permissions = await commandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
                var discordCommand = interactionService.SlashCommands.FirstOrDefault(c => c.Name.Equals(command, StringComparison.OrdinalIgnoreCase));
                if (discordCommand is null)
                {
                    await RespondAsync($"The command '{command}' does not exist in the Bot");
                    return;
                }
                var commandPermissions = permissions.GetOrAddCommand(discordCommand);
                if (commandPermissions is null)
                {
                    await RespondAsync($"The command '{command}' does not integrate into the Permissions System");
                    return;
                }
                if (commandPermissions.Channels.IsChannelAllowed(channel))
                {
                    commandPermissions.Channels.Allowed.Remove(channel.Id);
                }
                if (!commandPermissions.Channels.IsChannelBlocked(channel))
                {
                    commandPermissions.Channels.Blocked.Add(channel.Id);
                }
                await RespondAsync($"The command '{command}' has been disabled for <#{channel.Id}>");
            }

            [SlashCommand("allowgrp", "Enables a group of commands for the given room")]
            public async Task HandleAllowGroup(
                [Summary("channel", "The channel to explicitly enable a group for")] ITextChannel channel,
                [Summary("group", "The group to allow")] string group)
            {
                var permissions = await commandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
                var commands = interactionService.SlashCommands
                    .Where(c => (!string.IsNullOrEmpty(c.Module.SlashGroupName) && c.Module.SlashGroupName.Equals(group, StringComparison.OrdinalIgnoreCase)) || IsCommandParentInGroup(c.Module, group))
                    .Distinct(new CommandInfoComparer())
                    .ToList();

                if (!commands.Any())
                {
                    await RespondAsync($"The group '{group}' does not exist in the Bot");
                    return;
                }

                var responseBuilder = new StringBuilder();
                foreach (var discordCommand in commands)
                {
                    var commandPermissions = permissions.GetOrAddCommand(discordCommand);
                    if (commandPermissions is null)
                    {
                        responseBuilder.AppendLine($"The command '{discordCommand.GetFullCommandPath()}' does not integrate into the Permissions System");
                        continue;
                    }
                    if (!commandPermissions.Channels.IsChannelAllowed(channel))
                    {
                        commandPermissions.Channels.Allowed.Add(channel.Id);
                    }
                    if (commandPermissions.Channels.IsChannelBlocked(channel))
                    {
                        commandPermissions.Channels.Blocked.Remove(channel.Id);
                    }
                    responseBuilder.AppendLine($"The command '{discordCommand.GetFullCommandPath()}' has been enabled for <#{channel.Id}>");
                }
                await RespondAsync(responseBuilder.ToString());
            }

            [SlashCommand("denygrp", "Disables a group of commands for a given room")]
            public async Task HandleDenyGroup(
                [Summary("channel", "The channel to explicitly deny a group for")] ITextChannel channel,
                [Summary("group", "The group to deny")] string group)
            {
                var permissions = await commandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
                var commands = interactionService.SlashCommands
                    .Where(c => (!string.IsNullOrEmpty(c.Module.SlashGroupName) && c.Module.SlashGroupName.Equals(group, StringComparison.OrdinalIgnoreCase)) || IsCommandParentInGroup(c.Module, group))
                    .Distinct(new CommandInfoComparer())
                    .ToList();

                if (!commands.Any())
                {
                    await RespondAsync($"The group '{group}' does not exist in the Bot");
                    return;
                }

                var responseBuilder = new StringBuilder();
                foreach (var discordCommand in commands)
                {
                    var commandPermissions = permissions.GetOrAddCommand(discordCommand);
                    if (commandPermissions is null)
                    {
                        responseBuilder.AppendLine($"The command '{discordCommand.GetFullCommandPath()}' does not integrate into the Permissions System");
                        continue;
                    }
                    if (commandPermissions.Channels.IsChannelAllowed(channel))
                    {
                        commandPermissions.Channels.Allowed.Remove(channel.Id);
                    }
                    if (!commandPermissions.Channels.IsChannelBlocked(channel))
                    {
                        commandPermissions.Channels.Blocked.Add(channel.Id);
                    }
                    responseBuilder.AppendLine($"The command '{discordCommand.GetFullCommandPath()}' has been disabled for <#{channel.Id}>");
                }

                await RespondAsync(responseBuilder.ToString());
            }


            private bool IsCommandParentInGroup(ModuleInfo command, string group)
            {
                if (command.Parent is not null)
                {
                    return IsCommandParentInGroup(command.Parent, group);
                }
                if (string.IsNullOrWhiteSpace(command.SlashGroupName)) return false;
                return command.SlashGroupName.Equals(group, StringComparison.OrdinalIgnoreCase);
            }

            private sealed class CommandInfoComparer : IEqualityComparer<ICommandInfo>
            {
                public bool Equals(ICommandInfo x, ICommandInfo y)
                {
                    return x.GetFullCommandPath().Equals(y.GetFullCommandPath());
                }

                public int GetHashCode([DisallowNull] ICommandInfo obj)
                {
                    return obj.GetFullCommandPath().GetHashCode();
                }
            }

            private sealed class IntegratedCommandsAutocompleteHandler : AutocompleteHandler
            {
                public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
                    IInteractionContext context,
                    IAutocompleteInteraction autocompleteInteraction,
                    IParameterInfo parameter,
                    IServiceProvider services)
                {
                    var commandPermissionsService = services.GetRequiredService<CommandPermissionsService>();
                    var permissions = await commandPermissionsService.GetOrCreatePermissionsAsync(context.Guild.Id);
                    var commands = permissions.Permissions.Select(c => c.Command).ToList();
                    return AutocompletionResult.FromSuccess(commands.Select(c => new AutocompleteResult(c, c)));
                }
            }

        }

        /// <summary>
        /// Sub-module for the auto-communication shit
        /// </summary>
        [Group("communications", "Auto-communication messages")]
        public sealed class CommunicationModule : InteractionModuleBase
        {
            private readonly ChannelCommunicationService channelCommunicationService;
            private readonly InteractionHandlingService interactionHandlingService;

            public CommunicationModule(
                ChannelCommunicationService channelCommunicationService, InteractionHandlingService interactionHandlingService)
            {
                this.channelCommunicationService = channelCommunicationService ?? throw new ArgumentNullException(nameof(channelCommunicationService));
                this.interactionHandlingService = interactionHandlingService ?? throw new ArgumentNullException(nameof(interactionHandlingService));
            }


            [SlashCommand("create", "Creates a repeatable message to run on the given CRON job", runMode: RunMode.Async), RequireContext(ContextType.Guild)]
            public async Task ScheduleRepeatableMessage()
            {
                var callbackId = $"repeat-message-{Guid.NewGuid()}";
                var modalBuilder = new ModalBuilder()
                    .WithTitle($"Create Repeatable Message (for channel #{Context.Channel.Name})")
                    .WithCustomId(callbackId)
                    .AddTextInput("Message Name", "job-name", placeholder: "A unique name for the job. If left blank, a random ID will be generated instead")
                    .AddTextInput("CRON expression", "cron-string", placeholder: "A valid CRON expression", required: true)
                    .AddTextInput("Message", "repeat-message", placeholder: "This is the message you will have sent", required: true);

                interactionHandlingService.RegisterCallbackHandler(callbackId, new InteractionModalCallbackProvider(async (context) =>
                {
                    var jobName = context.Data.Components.First(d => d.CustomId.Equals("job-name", StringComparison.OrdinalIgnoreCase)).Value;
                    var cron = context.Data.Components.First(d => d.CustomId.Equals("cron-string", StringComparison.OrdinalIgnoreCase)).Value;
                    var message = context.Data.Components.First(d => d.CustomId.Equals("repeat-message", StringComparison.OrdinalIgnoreCase)).Value;

                    if (string.IsNullOrWhiteSpace(jobName))
                    {
                        jobName = Guid.NewGuid().ToString();
                    }

                    bool isCronExpression = false;
                    Cronos.CronExpression cronExpression = null;

                    try
                    {
                        cronExpression = Cronos.CronExpression.Parse(cron);
                        isCronExpression = true;
                    }
                    catch (Exception) { }

                    await channelCommunicationService.ScheduleNewTask(
                        Context.Guild,
                        new ChannelCommuncationJobEntry
                        {
                            ChannelId = Context.Channel.Id,
                            Created = DateTime.Now,
                            GuildId = Context.Guild.Id,
                            HasRun = false,
                            JobName = jobName,
                            Message = message,
                            Repeats = isCronExpression,
                            WhenToRun = cron
                        });

                    if (isCronExpression)
                    {
                        await context.RespondAsync($"Your job has been scheduled successfully and will run at {cronExpression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"))}. Job ID: {jobName}", ephemeral: true);
                    }
                    else
                    {
                        await context.RespondAsync($"Your job has been scheduled successfully and will run in approximately {cron} minute(s). Job ID: {jobName}", ephemeral: true);
                    }
                }, true));

                await RespondWithModalAsync(modalBuilder.Build());
            }

            [SlashCommand("remove", "Removes a scheduled message", runMode: RunMode.Async), RequireContext(ContextType.Guild)]
            public async Task RemoveRepeatableMessage(
                [Summary("Job", "The name of the job to cancel"), Autocomplete(typeof(MessageRemoverAutocompleteHandler))] string jobName)
            {
                // Falkenhoof: Instead of remove job why not put wolf job instead
                await channelCommunicationService.RemoveJob(Context.Guild, jobName);
                await RespondAsync($"Job {jobName} has been removed", ephemeral: true);
            }

        }

        private sealed class MessageRemoverAutocompleteHandler : AutocompleteHandler
        {
            public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
                IInteractionContext context,
                IAutocompleteInteraction autocompleteInteraction,
                IParameterInfo parameter,
                IServiceProvider services)
            {
                var ccs = services.GetRequiredService<ChannelCommunicationService>();
                var results = await ccs.GetServerJobs(context.Guild);
                return AutocompletionResult.FromSuccess(results.Select(r => new AutocompleteResult(r.JobName, r.JobName)));
            }
        }

    }

}
