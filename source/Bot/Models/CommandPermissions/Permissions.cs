using Bot.Preconditions;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Models.CommandPermissions
{

    /// <summary>
    ///     Defines an entry in the permission system
    /// </summary>
    public sealed class PermissionsEntry
    {
        public List<CommandEntry> Permissions { get; set; }

        public CommandEntry GetOrAddCommand(CommandInfo cmd)
        {
            var cmdPermissions = cmd.Preconditions.FirstOrDefault(a => a.GetType() == typeof(UseErectorPermissions));
            if (cmdPermissions is null)
            {
                return null;
            }
            var permissions = cmdPermissions as UseErectorPermissions;
            if (Permissions.FirstOrDefault(c => c.Command.Equals(cmd.GetFullCommandPath(), StringComparison.OrdinalIgnoreCase)) is null)
            {
                Permissions.Add(new CommandEntry
                {
                    Channels = new CommandChannelDetails
                    {
                        Allowed = new List<ulong>(),
                        Blocked = new List<ulong>()
                    },
                    Command = cmd.GetFullCommandPath(),
                    Default = permissions.Default,
                    Private = permissions.Private
                });
            }
            return Permissions.First(c => c.Command.Equals(cmd.GetFullCommandPath(), StringComparison.OrdinalIgnoreCase));
        }

    }

}