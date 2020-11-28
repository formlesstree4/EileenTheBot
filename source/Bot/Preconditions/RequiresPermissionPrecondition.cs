using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Models.CommandPermissions;
using Bot.Services;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Bot.Preconditions
{

    /// <summary>
    ///     Use this attribute to integrate your command into the permissions system.
    /// </summary>
    public sealed class RequiresPermissionPrecondition : PreconditionAttribute
    {

        /// <summary>
        ///     Gets or sets whether this commands default value.
        /// </summary>
        /// <remarks>This value is only used when inserting this command into the permissions system</remarks>
        public bool Default { get; set; } = true;

        /// <summary>
        ///     Gets or sets whether this commands default value for private usage.
        /// </summary>
        /// <remarks>This value is only used when inserting this command into the permissions system</remarks>
        public bool Private { get; set; } = true;

        public override async Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context,
            CommandInfo command,
            IServiceProvider services)
        {
            var permissions = services.GetRequiredService<CommandPermissionsService>();
            var serverPermissions = await permissions.GetOrCreatePermissionsAsync(context.Guild.Id);
            var commandPermission = serverPermissions.Permissions.FirstOrDefault(c => c.Command.Equals(command.Name, StringComparison.OrdinalIgnoreCase));
            if (commandPermission is null)
            {
                // add it to the server
                serverPermissions.Permissions.Add(new CommandEntry
                {
                    Channels = new CommandChannelDetails
                    {
                        Allowed = new List<ulong>(),
                        Blocked = new List<ulong>()
                    },
                    Command = command.Name,
                    Default = this.Default,
                    Private = this.Private
                });

                return this.Default ? PreconditionResult.FromSuccess() : PreconditionResult.FromError("This command requires permission to run in this room");
            }

            // Not null. Let's look at the details and see what's going on.
            if (commandPermission.Channels.IsChannelBlocked(context.Channel))
                return PreconditionResult.FromError("This command is not allowed to run in the given channel");
            if (!commandPermission.Default && !commandPermission.Channels.IsChannelAllowed(context.Channel))
                return PreconditionResult.FromError("This command is not allowed to run in the given channel");
            if (!commandPermission.Private && (context.Channel is IPrivateChannel || context.Channel is IDMChannel))
                return PreconditionResult.FromError("This command is not allowed to run in the given channel");
            return PreconditionResult.FromSuccess();
        }
    }
}