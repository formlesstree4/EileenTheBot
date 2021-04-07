using Bot.Services;
using Discord.Commands;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Modules
{


    /// <summary>
    ///     
    /// </summary>
    public sealed class PermissionsModule : ModuleBase<SocketCommandContext>
    {

        public CommandPermissionsService CommandPermissionsService { get; set; }

        public CommandService CommandService { get; set; }


        [Command("allowcmd")]
        [Summary("Enables a command for a particular room. If a command does NOT hook into the command permissions system, this will do nothing.")]
        [RequireUserPermission(Discord.GuildPermission.ManageChannels)]
        [RequireContext(ContextType.Guild)]
        public async Task HandleAllowCommand([Summary("The command to allow")] string command)
        {
            var permissions = await CommandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
            var discordCommand = CommandService.Commands.FirstOrDefault(c => c.Name.Equals(command, StringComparison.OrdinalIgnoreCase));
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

        [Command("denycmd")]
        [Summary("Disables a command for a particular room. If a command does NOT hook into the command permissions system, this will do nothing.")]
        [RequireUserPermission(Discord.GuildPermission.ManageChannels)]
        public async Task HandleDenyCommand([Summary("The command to deny")] string command)
        {
            var permissions = await CommandPermissionsService.GetOrCreatePermissionsAsync(Context.Guild.Id);
            var discordCommand = CommandService.Commands.FirstOrDefault(c => c.Name.Equals(command, StringComparison.OrdinalIgnoreCase));
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

    }

}