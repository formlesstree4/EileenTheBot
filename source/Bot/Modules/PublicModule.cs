using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bot.Services;
using Discord;
using Discord.Commands;

namespace Bot.Modules
{
    // Modules must be public and inherit from an IModuleBase
    public class PublicModule : ModuleBase<SocketCommandContext>
    {

        public CommandService Commands { get; set; }

        public BetterPaginationService PaginationService { get; set; }

        public StupidTextService StupidTextService { get; set; }

        [Command("help")]
        public async Task HelpAsync()
        {
            var embeds = new List<Embed>();
            foreach (var c in Commands.Commands)
            {
                var builder = new EmbedBuilder()
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName(Context.User.Username)
                        .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()))
                    .WithColor(new Color(152, 201, 124))
                    .WithDescription(c.Summary)
                    .WithCurrentTimestamp()
                    .WithTitle(c.Name)
                    .WithFooter(new EmbedFooterBuilder()
                        .WithText(StupidTextService.GetRandomStupidText()))
                    .AddField("NSFW", c.Preconditions.Any(p => p.GetType() == typeof(RequireNsfwAttribute)));
                embeds.Add(builder.Build());
            }
            await PaginationService.Send(Context.Channel, new BetterPaginationMessage(embeds, true, Context.User, "Command"));
        }

    }
}