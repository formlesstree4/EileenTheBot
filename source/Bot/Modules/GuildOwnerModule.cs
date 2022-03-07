using Bot.Services;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Modules
{

    [Group("guild"), RequireUserPermission(GuildPermission.ManageGuild), RequireContext(ContextType.Guild),
        Summary("Guild Owner specific commands for managing and configuring the Guild")]
    public sealed class GuildOwnerModule : ModuleBase<SocketCommandContext>
    {
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly ReactionHelperService reactionHelperService;


        public GuildOwnerModule(
            ServerConfigurationService serverConfigurationService,
            ReactionHelperService reactionHelperService)
        {
            this.serverConfigurationService = serverConfigurationService ?? throw new ArgumentNullException(nameof(serverConfigurationService));
            this.reactionHelperService = reactionHelperService ?? throw new ArgumentNullException(nameof(reactionHelperService));

        }

        [Command("prefix")]
        [Summary("Alters the character prefix the bot will use in order to be triggered")]
        public async Task SetPrefixAsync([Summary("The new prefix to use")] char prefix)
        {
            var configuration = await serverConfigurationService.GetOrCreateConfigurationAsync(Context.Guild);
            configuration.CommandPrefix = prefix;
            await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
        }


        [Group("permissions"), RequireUserPermission(GuildPermission.ManageChannels)]
        public sealed class PermissionsModule : ModuleBase<SocketCommandContext>
        {
            private readonly CommandPermissionsService commandPermissionsService;
            private readonly CommandService commandService;

            public PermissionsModule(
                CommandPermissionsService commandPermissionsService,
                CommandService commandService)
            {
                this.commandPermissionsService = commandPermissionsService ?? throw new ArgumentNullException(nameof(commandPermissionsService));
                this.commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            }



            [Command("allow")]
            [Summary("Enables a command for a particular room. If a command does NOT hook into the command permissions system, this will do nothing.")]
            public async Task HandleAllowCommand([Summary("The command to allow")] string command)
            {
                var permissions = await commandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
                var discordCommand = commandService.Commands.FirstOrDefault(c => c.Name.Equals(command, StringComparison.OrdinalIgnoreCase));
                if (discordCommand is null)
                {
                    await ReplyAsync($"The command '{command}' does not exist in the Bot");
                    return;
                }
                var commandPermissions = permissions.GetOrAddCommand(discordCommand);
                if (commandPermissions is null)
                {
                    await ReplyAsync($"The command '{command}' does not integrate into the Permissions System");
                    return;
                }
                if (!commandPermissions.Channels.IsChannelAllowed(Context.Channel))
                {
                    commandPermissions.Channels.Allowed.Add(Context.Channel.Id);
                }
                if (commandPermissions.Channels.IsChannelBlocked(Context.Channel))
                {
                    commandPermissions.Channels.Blocked.Remove(Context.Channel.Id);
                }
                await ReplyAsync($"The command '{command}' has been enabled for <#{Context.Channel.Id}>");
            }

            [Command("deny")]
            [Summary("Disables a command for a particular room. If a command does NOT hook into the command permissions system, this will do nothing.")]
            public async Task HandleDenyCommand([Summary("The command to deny")] string command)
            {
                var permissions = await commandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
                var discordCommand = commandService.Commands.FirstOrDefault(c => c.Name.Equals(command, StringComparison.OrdinalIgnoreCase));
                if (discordCommand is null)
                {
                    await ReplyAsync($"The command '{command}' does not exist in the Bot");
                    return;
                }
                var commandPermissions = permissions.GetOrAddCommand(discordCommand);
                if (commandPermissions is null)
                {
                    await ReplyAsync($"The command '{command}' does not integrate into the Permissions System");
                    return;
                }
                if (commandPermissions.Channels.IsChannelAllowed(Context.Channel))
                {
                    commandPermissions.Channels.Allowed.Remove(Context.Channel.Id);
                }
                if (!commandPermissions.Channels.IsChannelBlocked(Context.Channel))
                {
                    commandPermissions.Channels.Blocked.Add(Context.Channel.Id);
                }
                await ReplyAsync($"The command '{command}' has been disabled for <#{Context.Channel.Id}>");
            }

            [Command("allowgrp")]
            public async Task HandleAllowGroup([Summary("The group to allow")] string group)
            {
                var permissions = await commandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
                var commands = commandService.Commands
                    .Where(c => (!string.IsNullOrEmpty(c.Module.Group) && c.Module.Group.Equals(group, StringComparison.OrdinalIgnoreCase)) || IsCommandParentInGroup(c.Module, group))
                    .Distinct(new CommandInfoComparer())
                    .ToList();

                if (!commands.Any())
                {
                    await ReplyAsync($"The group '{group}' does not exist in the Bot");
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
                    if (!commandPermissions.Channels.IsChannelAllowed(Context.Channel))
                    {
                        commandPermissions.Channels.Allowed.Add(Context.Channel.Id);
                    }
                    if (commandPermissions.Channels.IsChannelBlocked(Context.Channel))
                    {
                        commandPermissions.Channels.Blocked.Remove(Context.Channel.Id);
                    }
                    responseBuilder.AppendLine($"The command '{discordCommand.GetFullCommandPath()}' has been enabled for <#{Context.Channel.Id}>");
                }
                await ReplyAsync(responseBuilder.ToString());
            }

            [Command("denygrp")]
            public async Task HandleDenyGroup([Summary("The group to deny")] string group)
            {
                var permissions = await commandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
                var commands = commandService.Commands
                    .Where(c => (!string.IsNullOrEmpty(c.Module.Group) && c.Module.Group.Equals(group, StringComparison.OrdinalIgnoreCase)) || IsCommandParentInGroup(c.Module, group))
                    .Distinct(new CommandInfoComparer())
                    .ToList();

                if (!commands.Any())
                {
                    await ReplyAsync($"The group '{group}' does not exist in the Bot");
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
                    if (commandPermissions.Channels.IsChannelAllowed(Context.Channel))
                    {
                        commandPermissions.Channels.Allowed.Remove(Context.Channel.Id);
                    }
                    if (!commandPermissions.Channels.IsChannelBlocked(Context.Channel))
                    {
                        commandPermissions.Channels.Blocked.Add(Context.Channel.Id);
                    }
                    responseBuilder.AppendLine($"The command '{discordCommand.GetFullCommandPath()}' has been disabled for <#{Context.Channel.Id}>");
                }

                await ReplyAsync(responseBuilder.ToString());
            }


            private bool IsCommandParentInGroup(ModuleInfo command, string group)
            {
                if (command.Parent is not null)
                {
                    return IsCommandParentInGroup(command.Parent, group);
                }
                if (string.IsNullOrWhiteSpace(command.Group)) return false;
                return command.Group.Equals(group, StringComparison.OrdinalIgnoreCase);
            }

            private sealed class CommandInfoComparer : IEqualityComparer<CommandInfo>
            {
                public bool Equals(CommandInfo x, CommandInfo y)
                {
                    return x.GetFullCommandPath().Equals(y.GetFullCommandPath());
                }

                public int GetHashCode([DisallowNull] CommandInfo obj)
                {
                    return obj.GetFullCommandPath().GetHashCode();
                }
            }

        }

    }

}
