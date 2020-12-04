using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bot.Preconditions;
using Bot.Services;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Hangfire;
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

        public RavenDatabaseService Rdbs { get; set; }

        public MarkovService MarkovService { get; set; }

        public CancellationTokenSource TokenSource { get; set; }

        public UserService UserService { get; set; }

        public ReactionHelperService ReactionHelperService { get; set; }


        [Command("help")]
        public async Task HelpAsync()
        {
            var embeds = new List<Embed>();
            foreach (var c in Commands.Commands.Where(c => !c.Preconditions.Any(f => f.GetType() == typeof(TrustedUsersPrecondition))))
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


        [Command("run")]
        [Summary("Runs a recurring job that the server maintains")]
        [TrustedUsersPrecondition]
        public async Task RunRecurringJob([Summary("The unique name of the job to run")]string jobName)
        {
            try
            {
                RecurringJob.Trigger(jobName);
                await ReactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
            }
            catch
            {
                await ReactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
            }
        }

        #pragma warning disable CS1998
        [Command("poem")]
        [Summary("Generates a poem with an optional title")]
        public async Task PoetryAsync(
            [Name("Title"),
            Summary("The optional title of the poem"),
            Remainder]string title = "")
        {
            var url = Rdbs.Configuration.GptUrl;
            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out var t)) return;
            url = url.Replace(t.Port.ToString(), "8081");
            #pragma warning disable CS4014
            Task.Factory.StartNew(async() => {
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
            });
            #pragma warning restore CS4014
        }
        #pragma warning restore CS1998

        [Command("kill"), TrustedUsersPrecondition]
        public async Task KillAsync()
        {
            await Context.Channel.SendMessageAsync("Initiating shutdown request...");
            TokenSource.Cancel();
        }

        private static string BoolToYesNo(bool b) => b ? "Yes": "No";

        private static string GetContext(RequireContextAttribute attribute)
        {
            if (attribute is null) return "None";
            return attribute.Contexts.ToString();
        }


    }
}