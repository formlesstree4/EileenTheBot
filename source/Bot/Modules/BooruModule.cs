using AutoMapper;
using Bot.Models;
using Bot.Services;
using Bot.Services.Booru;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Modules
{

    public sealed class BooruModule : ModuleBase<SocketCommandContext>
    {

        private const string NsfwErrorMessage = "uwu oopsie-woopsie you made a lil fucksy-wucksy and twied to be lewdie in pubwic";

        private const string NoResultsMessage = "";

        private const string CriteriaSummary = "The collection of booru-safe tags. Tags with multiple words use underscores instead of spaces (such as long_hair)";

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
        private readonly Func<LogMessage, Task> logger;
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
            Func<LogMessage, Task> logger)
        {
            this.paginationService = paginationService ?? throw new ArgumentNullException(nameof(paginationService));
            this.danbooru = danbooru ?? throw new ArgumentNullException(nameof(danbooru));
            this.e621 = e621 ?? throw new ArgumentNullException(nameof(e621));
            this.gelbooru = gelbooru ?? throw new ArgumentNullException(nameof(gelbooru));
            this.safeBooru = safeBooru ?? throw new ArgumentNullException(nameof(safeBooru));
            this.yandere = yandere ?? throw new ArgumentNullException(nameof(yandere));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.stupidTextService = stupidTextService ?? throw new ArgumentNullException(nameof(stupidTextService));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }



        [Command("aliases")]
        [Summary("Lists all the currently available tag aliases")]
        public Task ListTagAliasesAsync()
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("The following aliases are available");
            foreach (var c in tagAliasesDesc)
            {
                messageBuilder.AppendLine($"\t{c}");
            }
            messageBuilder.AppendLine($"Example: `.db long_hair --take 1` will return the first discovered `long_hair` image on Danbooru");
            return ReplyAsync(messageBuilder.ToString());
        }

        [Command("db")]
        [Summary("Invokes the Danbooru API. If performed in SFW channels, the '-s' tag is automatically appended")]
        [RequireContext(ContextType.Guild, ErrorMessage = ContextErrorMessage)]
        public async Task DanbooruSearchAsync(
            [Summary(CriteriaSummary)] params string[] criteria)
        {
            await InitialCommandHandler(danbooru, criteria);
        }

        [Command("fur")]
        [Summary("Invokes the e621 API. If performed in SFW channels, the '-s' tag is automatically appended")]
        [RequireContext(ContextType.Guild, ErrorMessage = ContextErrorMessage)]
        public async Task e621SearchAsync(
            [Summary(CriteriaSummary)] params string[] criteria)
        {
            await InitialCommandHandler(e621, criteria);
        }

        [Command("gb")]
        [Summary("Invokes the Gelbooru API. If performed in SFW channels, the '-s' tag is automatically appended")]
        [RequireContext(ContextType.Guild, ErrorMessage = ContextErrorMessage)]
        public async Task GelbooruSearchAsync(
            [Summary(CriteriaSummary)] params string[] criteria)
        {
            await InitialCommandHandler(gelbooru, criteria);
        }

        [Command("sb")]
        [Summary("Invokes the Safebooru API. If performed in SFW channels, the '-s' tag is automatically appended")]
        [RequireContext(ContextType.Guild, ErrorMessage = ContextErrorMessage)]
        public async Task SafebooruSearchAsync(
            [Summary(CriteriaSummary)] params string[] criteria)
        {
            await InitialCommandHandler(safeBooru, criteria);
        }

        [Command("yan")]
        [Summary("Invokes the Yande.re API. If performed in SFW channels, the '-s' tag is automatically appended")]
        [RequireContext(ContextType.Guild, ErrorMessage = ContextErrorMessage)]
        public async Task YandereSearchAsync(
            [Summary(CriteriaSummary)] params string[] criteria)
        {
            await InitialCommandHandler(yandere, criteria);
        }



        private async Task InitialCommandHandler<TResponse, T>(
            BooruService<TResponse, T> service,
            params string[] criteria)
        {
            var newCriteria = ExpandCriteria(criteria);
            var parameters = GetSkipAndTake(ref newCriteria);

            var pageNumber = parameters[SkipParameter];
            var pageSize = parameters[TakeParameter];

            var results = (await service.SearchAsync(pageSize, pageNumber, newCriteria)).ToList();
            var posts = results.Select(c => mapper.Map<T, EmbedPost>(c));
            await PostAsync(posts, newCriteria, pageNumber);
        }

        private async Task PostAsync(IEnumerable<EmbedPost> results, string[] criteria, int pageNumber)
        {
            var messages = new List<Embed>();
            using var ts = Context.Channel.EnterTypingState();
            if (!results.Any())
            {
                await ReplyAsync($"uwu oopsie-woopsie you made a lil fucksy-wucksy with your inqwery sooo I have nothing to showy-wowie! (Searched using: {string.Join(", ", criteria)})");
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
                            .WithName("Search Results")
                            .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()))
                        .WithColor(new Color(152, 201, 124))
                        .WithCurrentTimestamp()
                        .WithImageUrl(booruPost.ImageUrl)
                        .WithTitle($"The Good Stuff")
                        .WithFooter($"{stupidTextService.GetRandomStupidText()} | Page Offset: {pageNumber}")
                        .WithUrl(booruPost.PageUrl);
                    messages.Add(eBuilder.Build());
                }
                catch (ArgumentException are)
                {
                    Write($"Failed to create a Booru post: {are}", severity: LogSeverity.Error);
                    continue;
                }
            }
            await paginationService.Send(Context.Channel, new BetterPaginationMessage(messages, true, Context.User, "Image") { IsNsfw = true });
        }

        private string[] ExpandCriteria(string[] c)
        {
            var tags = new List<string>(c);
            var results = new List<string>();
            if (Context.Channel is ITextChannel t && !t.IsNsfw) tags.Add("-s");
            foreach (var i in tags) results.Add(tagAliases.TryGetValue(i.ToLowerInvariant(), out var alias) ? alias : i);
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
                switch (c[index].ToLowerInvariant())
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

        private void Write(
            string message,
            string source = nameof(BooruModule),
            LogSeverity severity = LogSeverity.Info)
        {
            logger(new LogMessage(severity, source, message));
        }

    }

}
