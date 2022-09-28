using AutoMapper;
using Bot.Models.Booru;
using Bot.Services;
using Bot.Services.Booru;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Modules
{

    [Group("booru", "Searches various image boorus")]
    public sealed class BooruModule : InteractionModuleBase
    {

        private const string NsfwErrorMessage = "uwu oopsie-woopsie you made a lil fucksy-wucksy and twied to be lewdie in pubwic";

        private const string NoResultsMessage = "";

        private const string CriteriaSummary = "Criteria. Tags with multiple words use underscores instead of spaces";

        private const string ContextErrorMessage = "uwu pubwic channels ownly~";

        private const string TakeParameter = "take";

        private const string SkipParameter = "skip";

        private readonly BetterPaginationService paginationService;
        private readonly Danbooru danbooru;
        private readonly e621 e621;
        private readonly Gelbooru gelbooru;
        private readonly SafeBooru safeBooru;
        private readonly Yandere yandere;
        private readonly IMapper mapper;
        private readonly StupidTextService stupidTextService;
        private readonly ILogger<BooruModule> logger;
        private static readonly IReadOnlyDictionary<string, string> tagAliases = new Dictionary<string, string>
        {
            ["-r"] = "order:random",
            ["-e"] = "rating:explicit",
            ["-q"] = "rating:questionable",
            ["-s"] = "rating:safe"
        };

        private static readonly IReadOnlyDictionary<string, string> tagAliasesDesc = new Dictionary<string, string>
        {
            ["-r"] = "Adds a random order flag",
            ["-e"] = "Enforces explicit only",
            ["-q"] = "Enforces questionable only",
            ["-s"] = "Enforces safe only",
            ["--skip"] = "Skip 'n' number of pages",
            ["--take"] = "Take 'n' number of posts on said page"
        };


        public BooruModule(
            BetterPaginationService paginationService,
            Danbooru danbooru,
            e621 e621,
            Gelbooru gelbooru,
            SafeBooru safeBooru,
            Yandere yandere,
            IMapper mapper,
            StupidTextService stupidTextService,
            ILogger<BooruModule> logger)
        {
            this.paginationService = paginationService ?? throw new ArgumentNullException(nameof(paginationService));
            this.danbooru = danbooru ?? throw new ArgumentNullException(nameof(danbooru));
            this.e621 = e621 ?? throw new ArgumentNullException(nameof(e621));
            this.gelbooru = gelbooru ?? throw new ArgumentNullException(nameof(gelbooru));
            this.safeBooru = safeBooru ?? throw new ArgumentNullException(nameof(safeBooru));
            this.yandere = yandere ?? throw new ArgumentNullException(nameof(yandere));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.stupidTextService = stupidTextService ?? throw new ArgumentNullException(nameof(stupidTextService));
            this.logger = logger;
        }



        [SlashCommand("aliases", "Lists all the currently available tag aliases")]
        public Task ListTagAliasesAsync()
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("The following aliases are available");
            foreach (var c in tagAliasesDesc)
            {
                messageBuilder.AppendLine($"{c}");
            }
            messageBuilder.AppendLine($"Example: `.db long_hair --take 1` will return the first discovered `long_hair` image on Danbooru");

            var embedBuilder = new EmbedBuilder()
                .WithAuthor(Context.User.Username)
                .WithTitle("Aliases")
                .WithDescription(messageBuilder.ToString())
                .WithColor(Color.Green)
                .WithCurrentTimestamp();
            return RespondAsync(embed: embedBuilder.Build());
        }

        [SlashCommand("danbooru", "Invokes the Danbooru API.", runMode: RunMode.Async)]
        [RequireContext(ContextType.Guild)]
        public async Task DanbooruSearchAsync([Summary("criteria", CriteriaSummary)] string criteria)
        {
            await InitialCommandHandler(danbooru, criteria);
        }

        [SlashCommand("fur", "Invokes the e621 API.", runMode: RunMode.Async)]
        [RequireContext(ContextType.Guild)]
        public async Task e621SearchAsync(
            [Summary("criteria", CriteriaSummary)] string criteria)
        {
            await InitialCommandHandler(e621, criteria);
        }

        [SlashCommand("gb", "Invokes the Gelbooru API.", runMode: RunMode.Async)]
        [RequireContext(ContextType.Guild)]
        public async Task GelbooruSearchAsync(
            [Summary("criteria", CriteriaSummary)] string criteria)
        {
            await InitialCommandHandler(gelbooru, criteria);
        }

        [SlashCommand("sb", "Invokes the Safebooru API.", runMode: RunMode.Async)]
        [RequireContext(ContextType.Guild)]
        public async Task SafebooruSearchAsync(
            [Summary("criteria", CriteriaSummary)] string criteria)
        {
            await InitialCommandHandler(safeBooru, criteria);
        }

        [SlashCommand("yan", "Invokes the Yande.re API.", runMode: RunMode.Async)]
        [RequireContext(ContextType.Guild)]
        public async Task YandereSearchAsync(
            [Summary("criteria", CriteriaSummary)] string criteria)
        {
            await InitialCommandHandler(yandere, criteria);
        }



        private async Task InitialCommandHandler<TResponse, T>(
            BooruService<TResponse, T> service,
            string criteria)
        {
            var newCriteria = ExpandCriteria(criteria);
            var parameters = GetSkipAndTake(ref newCriteria);

            var pageNumber = parameters[SkipParameter];
            var pageSize = parameters[TakeParameter];

            var results = (await service.SearchAsync(pageSize, pageNumber, newCriteria)).ToList();
            var posts = results.Select(c => mapper.Map<T, EmbedPost>(c));
            await PostAsync(service, posts, newCriteria, pageNumber);
        }

        private async Task PostAsync<TResponse, T>(BooruService<TResponse, T> service, IEnumerable<EmbedPost> results, string[] criteria, int pageNumber)
        {
            var messages = new List<Embed>();
            using var ts = Context.Channel.EnterTypingState();
            if (!results.Any())
            {
                await RespondAsync($"uwu oopsie-woopsie you made a lil fucksy-wucksy with your inqwery sooo I have nothing to showy-wowie! (Searched using: {string.Join(", ", criteria)})");
                return;
            }
            foreach (var booruPost in results)
            {
                try
                {
                    var artistName = booruPost.ArtistName;
                    var eBuilder = new EmbedBuilder()
                        .AddField("Criteria", string.Join(", ", criteria), true)
                        .AddField("Artist(s)", artistName, true)
                        .WithAuthor(new EmbedAuthorBuilder()
                            .WithName($"Search Results for {Context.User.Username}#{Context.User.Discriminator}")
                            .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()))
                        .WithColor(new Color(152, 201, 124))
                        .WithCurrentTimestamp()
                        .WithImageUrl(booruPost.ImageUrl)
                        .WithTitle($"Booru: {service.Name}")
                        .WithFooter($"{stupidTextService.GetRandomStupidText()} | Page Offset: {pageNumber}")
                        .WithUrl(booruPost.PageUrl);
                    messages.Add(eBuilder.Build());
                }
                catch (ArgumentException are)
                {
                    logger.LogError(are, "Failed to create a Booru Post");
                    continue;
                }
            }
            await paginationService.Send(Context, Context.Channel, new BetterPaginationMessage(messages, true, Context.User, "Image") { IsNsfw = true });
        }

        private string[] ExpandCriteria(string d)
        {
            var c = d.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            var tags = new List<string>(c);
            var results = new List<string>();
            if (Context.Channel is ITextChannel t && !t.IsNsfw) tags.Add("-s");
            foreach (var i in tags) results.Add(tagAliases.TryGetValue(i.ToLower(), out var alias) ? alias : i);
            return results.ToArray();
        }

        private static IReadOnlyDictionary<string, int> GetSkipAndTake(ref string[] c)
        {
            var updated = new List<string>();
            var results = new Dictionary<string, int>
            {
                [TakeParameter] = 50,
                [SkipParameter] = 1
            };
            for (var index = 0; index < c.Length; index++)
            {
                switch (c[index].ToLower())
                {
                    case "--" + TakeParameter:
                        if (int.TryParse(c[index + 1], out var t))
                        {
                            results[TakeParameter] = t;
                        }
                        index++;
                        break;
                    case "--" + SkipParameter:
                        if (int.TryParse(c[index + 1], out var s))
                        {
                            results[SkipParameter] = s;
                        }
                        index++;
                        break;
                    default:
                        updated.Add(c[index]);
                        break;
                }
            }
            c = updated.ToArray();
            return results;
        }

    }

}
