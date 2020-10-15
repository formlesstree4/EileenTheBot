using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bot.Services;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;

namespace Bot.Modules
{
    // Modules must be public and inherit from an IModuleBase
    public class PublicModule : ModuleBase<SocketCommandContext>
    {

        private static HttpClient client = new HttpClient();

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

        [Command("poem")]
        [Summary("Generates a poem with an optional title")]
        public async Task PoetryAsync(
            [Name("Title"),
            Summary("The optional title of the poem"),
            Remainder]string title = "")
        {
            var url = System.Environment.GetEnvironmentVariable("GptUrl");
            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out var t)) return;
            url = url.Replace(t.Port.ToString(), "8081");
            using (Context.Channel.EnterTypingState())
            {
                var payload = new { title = (title ?? "") };
                var responseType = new { text = "" };
                var message = JsonConvert.SerializeObject(payload);
                var content = new StringContent(message);
                var jsonResponse = await client.PostAsync(url, content);
                var poetry = JsonConvert.DeserializeAnonymousType(await jsonResponse.Content.ReadAsStringAsync(), responseType);
                var responseString = $"```\r\n{poetry.text}\r\n```";
                await Context.Channel.SendMessageAsync(responseString);
            }
        }


        private static string BoolToYesNo(bool b) => b ? "Yes": "No";

        private static string GetContext(RequireContextAttribute attribute)
        {
            if (attribute is null) return "None";
            return attribute.Contexts.ToString();
        }

    }
}