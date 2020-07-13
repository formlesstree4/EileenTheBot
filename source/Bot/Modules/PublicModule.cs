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
                    .AddField("Requires NSFW", BoolToYesNo(c.Preconditions.Any(p => p.GetType() == typeof(RequireNsfwAttribute))), true)
                    .AddField("Required Context", GetContext((RequireContextAttribute)c.Preconditions.FirstOrDefault(p => p.GetType() == typeof(RequireContextAttribute))), true);

                foreach (var p in c.Parameters)
                {
                    builder.AddField(p.Name, p.Summary, true);
                }
                embeds.Add(builder.Build());
            }
            await PaginationService.Send(Context.Channel, new BetterPaginationMessage(embeds, true, Context.User, "Command"));
        }



        private static string BoolToYesNo(bool b) => b ? "Yes": "No";

        private static string GetContext(RequireContextAttribute attribute)
        {
            if (attribute is null) return "None";
            return attribute.Contexts.ToString();
        }

    }
}