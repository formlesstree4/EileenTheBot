using Bot.Preconditions;
using Bot.Services;
using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Bot.Modules
{
    // Modules must be public and inherit from an IModuleBase
    public class GlobalModule : ModuleBase<SocketCommandContext>
    {

        private readonly DiceRollService rollService;

        public GlobalModule(
            DiceRollService rollService)
        {
            this.rollService = rollService ?? throw new System.ArgumentNullException(nameof(rollService));
        }

//#pragma warning disable CS1998
//        [Command("poem")]
//        [Summary("Generates a poem with an optional title")]
//        public async Task PoetryAsync(
//            [Name("Title"),
//            Summary("The optional title of the poem"),
//            Remainder]string title = "")
//        {
//            var config = await serverConfigurationService.GetOrCreateConfigurationAsync(Context.Guild);
//            if (config.ResponderType != Models.ServerConfigurationData.AutomatedResponseType.GPT) return;
//            var url = ravenDatabaseService.Configuration.GptUrl;
//            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out var t)) return;
//            url = url.Replace(t.Port.ToString(), "8081");
//#pragma warning disable CS4014
//            Task.Factory.StartNew(async () =>
//            {
//                using (Context.Channel.EnterTypingState())
//                {
//                    var payload = new { title = (title ?? "") };
//                    var responseType = new { text = "" };
//                    var message = JsonConvert.SerializeObject(payload);
//                    var content = new StringContent(message);
//                    var jsonResponse = await client.PostAsync(url, content);
//                    var poetry = JsonConvert.DeserializeAnonymousType(await jsonResponse.Content.ReadAsStringAsync(), responseType);
//                    var responseString = $"```\r\n{poetry.text}\r\n```";
//                    await Context.Channel.SendMessageAsync(responseString);
//                }
//            });
//#pragma warning restore CS4014
//        }
//#pragma warning restore CS1998


        [Command("roll")]
        [Summary("Performs a dice roll")]
        public async Task RollAsync(
            [Name("Expression"),
            Summary("The die expression to roll"),
            Remainder]string expression)
        {
            var expr = rollService.GetDiceExpression(expression);
            await ReplyAsync($"Rolling {expr}...");
            await ReplyAsync($"Result: {expr.Evaluate():N0}");
        }

    }


    [Group("help")]
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly BetterPaginationService betterPaginationService;
        private readonly CommandService commandService;
        private readonly StupidTextService stupidTextService;
        private readonly ServiceManager serviceManager;

        public HelpModule(
            BetterPaginationService betterPaginationService,
            CommandService commandService,
            StupidTextService stupidTextService,
            ServiceManager serviceManager)
        {
            this.betterPaginationService = betterPaginationService ?? throw new System.ArgumentNullException(nameof(betterPaginationService));
            this.commandService = commandService ?? throw new System.ArgumentNullException(nameof(commandService));
            this.stupidTextService = stupidTextService ?? throw new System.ArgumentNullException(nameof(stupidTextService));
            this.serviceManager = serviceManager ?? throw new System.ArgumentNullException(nameof(serviceManager));
        }

        [Command, Summary("Lists out all the commands (and their aliases)")]
        public async Task HelpAsync()
        {
            var embeds = new List<Embed>();
            foreach (var c in commandService.Commands.Where(c => !c.Preconditions.Any(f => f.GetType() == typeof(TrustedUsersPrecondition))))
            {
                var attributes = string.Join(",", c.Attributes.Select(f => f.GetType().Name));
                if (string.IsNullOrWhiteSpace(attributes)) attributes = "None";
                var builder = new EmbedBuilder()
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName(Context.User.Username)
                        .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()))
                    .WithColor(new Color(152, 201, 124))
                    .WithDescription(c.Summary)
                    .WithCurrentTimestamp()
                    .WithTitle(c.Name)
                    .WithFooter(new EmbedFooterBuilder()
                        .WithText(stupidTextService.GetRandomStupidText()))
                    .AddField("Requires NSFW", BoolToYesNo(c.Preconditions.Any(p => p.GetType() == typeof(RequireNsfwAttribute))), true)
                    .AddField("Required Context", GetContext((RequireContextAttribute)c.Preconditions.FirstOrDefault(p => p.GetType() == typeof(RequireContextAttribute))), true)
                    .AddField("Attributes", attributes);

                foreach (var p in c.Parameters)
                {
                    builder.AddField(p.Name, p.Summary, true);
                }
                embeds.Add(builder.Build());
            }
            await betterPaginationService.Send(null, Context.Channel, new BetterPaginationMessage(embeds, true, Context.User, "Command"));
        }

        [Command("service"), Summary("Provides some information about a particular service and what it does")]
        public async Task ServiceHelpAsync([Summary("The service to look up information over")] string serviceName)
        {
            var serviceType = serviceManager.GetServiceType(serviceName);
            if (serviceType is null)
            {
                await ReplyAsync($"The service '{serviceName}' does not exist");
                return;
            }
            var attr = serviceType.GetCustomAttribute<SummaryAttribute>();
            if (attr is null)
            {
                await ReplyAsync($"The service '{serviceName}' has not provided a Summary");
                return;
            }
            await ReplyAsync($"{serviceName}: {attr.Text}");
        }

        private static string BoolToYesNo(bool b) => b ? "Yes" : "No";

        private static string GetContext(RequireContextAttribute attribute)
        {
            if (attribute is null) return "None";
            return attribute.Contexts.ToString();
        }

    }

}
