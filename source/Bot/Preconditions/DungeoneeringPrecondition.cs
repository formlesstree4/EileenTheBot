using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace Bot.Preconditions
{


    public sealed class RequiresDungeoneeringPrecondition : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context,
            CommandInfo command,
            IServiceProvider services)
        {
            throw new NotImplementedException();
        }
    }

}