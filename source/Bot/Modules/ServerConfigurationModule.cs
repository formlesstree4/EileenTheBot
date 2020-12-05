using System.Threading.Tasks;
using Bot.Services;
using Discord.Commands;

namespace Bot.Modules
{


    public sealed class ServerConfigurationModule : ModuleBase<SocketCommandContext>
    {

        public ServerConfigurationService ServerConfigurationService { get; set; }

        public ReactionHelperService ReactionHelperService { get; set; }

        [Command("prefix")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(Discord.GuildPermission.ManageGuild)]
        public async Task SetPrefixAsync([Summary("The new prefix to use")]char prefix)
        {
            var configuration = await ServerConfigurationService.GetOrCreateConfigurationAsync(Context.Guild);
            configuration.CommandPrefix = prefix;
            await ReactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
        }
        

    }


}