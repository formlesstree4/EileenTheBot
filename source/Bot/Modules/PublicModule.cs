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


        // [Remainder] takes the rest of the command's arguments as one argument, rather than splitting every space
        [Command("echo")]
        public Task EchoAsync([Remainder] string text)
            // Insert a ZWSP before the text to prevent triggering other bots!
            => ReplyAsync('\u200B' + text);

        // 'params' will parse space-separated elements into a list
        [Command("list")]
        public Task ListAsync(params string[] objects)
            => ReplyAsync("You listed: " + string.Join("; ", objects));


        [Command("embed")]
        public Task EmbedAsync(params string[] items)
            => ReplyAsync(embed: new EmbedBuilder()
                .AddField("test", string.Join(", ", items)).Build());

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


        //// Setting a custom ErrorMessage property will help clarify the precondition error
        // [Command("guild_only")]
        // [RequireContext(ContextType.Guild, ErrorMessage = "Sorry, this command must be ran from within a server, not a DM!")]
        // public Task GuildOnlyCommand()
        //     => ReplyAsync("Nothing to see here!");
    }
}