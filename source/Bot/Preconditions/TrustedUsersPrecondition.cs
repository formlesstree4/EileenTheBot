using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Bot.Services;

namespace Bot.Preconditions
{

    public sealed class TrustedUsersPrecondition : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckRequirementsAsync(
            IInteractionContext context,
            ICommandInfo commandInfo,
            IServiceProvider services)
        {
            var trustedUsers = services.GetRequiredService<TrustedUserService>();
            return Task.FromResult(trustedUsers.IsTrustedUser(context.User.Id)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError("You are not allowed to perform this command!"));
        }
    }
}
