using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Bot.Modules
{
    // Modules must be public and inherit from an IModuleBase
    public class PublicModule : ModuleBase<SocketCommandContext>
    {

        [Command("ping")]
        [Alias("pong", "hello")]
        public Task PingAsync()
            => ReplyAsync("pong!");


        // Get info on a user, or the user who invoked the command if one is not specified
        [Command("userinfo")]
        public async Task UserInfoAsync(IUser user = null)
        {
            user = user ?? Context.User;

            await ReplyAsync(user.ToString());
        }

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

        // // Setting a custom ErrorMessage property will help clarify the precondition error
        // [Command("guild_only")]
        // [RequireContext(ContextType.Guild, ErrorMessage = "Sorry, this command must be ran from within a server, not a DM!")]
        // public Task GuildOnlyCommand()
        //     => ReplyAsync("Nothing to see here!");
    }
}