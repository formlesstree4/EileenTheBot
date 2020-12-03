using System;
using System.Linq;
using System.Threading.Tasks;
using Bot.Services.RavenDB;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Bot.Preconditions
{

    public sealed class TrustedUsersPrecondition : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context,
            CommandInfo command,
            IServiceProvider services)
        {
            var ravenDatabaseService = services.GetRequiredService<RavenDatabaseService>();
            var configuration = ravenDatabaseService.Configuration;
            return await Task.FromResult(configuration.TrustedUsers.Contains(context.User.Id) ?
                PreconditionResult.FromSuccess():
                PreconditionResult.FromError("You are not allowed to perform this command!"));
        }
    }


}