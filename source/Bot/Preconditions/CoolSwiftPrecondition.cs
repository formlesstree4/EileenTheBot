using Discord;
using Discord.Interactions;
using System;
using System.Threading.Tasks;

namespace Bot.Preconditions
{
    public sealed class CoolSwiftPrecondition : PreconditionAttribute
    {

        private const ulong CoolswiftUserId = 143551309776289792;

        public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {
            return Task.FromResult(context.User.Id == CoolswiftUserId ?
                PreconditionResult.FromSuccess() :
                PreconditionResult.FromError("You are not allowed to perform this action"));
        }
    }
}
