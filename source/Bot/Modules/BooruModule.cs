using System.Threading.Tasks;
using Discord.Commands;
using Bot.Services;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;
using Bot.Services.Booru;
using AutoMapper;
using Bot.Models;
using System;

namespace Bot.Modules
{

    public sealed class BooruModule : ModuleBase<SocketCommandContext>
    {

        private const string NsfwErrorMessage = "uwu oopsie-woopsie you made a lil fucksy-wucksy and twied to be lewdie in pubwic";

        private static IReadOnlyDictionary<string, string> tagAliases = new Dictionary<string, string>
        {
            ["-r"] = "order:random",
            ["-e"] = "rating:explicit",
            ["-q"] = "rating:questionable",
            ["-s"] = "rating:safe"
        };

        private static IReadOnlyDictionary<string, string> tagAliasesDesc = new Dictionary<string, string>
        {
            ["-r"] = "Adds a random order flag",
            ["-e"] = "Enforces explicit only",
            ["-q"] = "Enforces questionable only",
            ["-s"] = "Enforces safe only",
            ["--skip"] = "Skip 'n' number of pages",
            ["--take"] = "Take 'n' number of posts on said page"
        };

        public BetterPaginationService PaginationService { get; set; }

        public Danbooru Danbooru { get; set; }

        public e621 e621 { get; set; }

        public Gelbooru Gelbooru { get; set; }

        public SafeBooru Safebooru { get; set; }

        public Yandere Yandere { get; set; }

        public IMapper Mapper { get; set; }



        [Command("aliases")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        public Task ListTagAliasesAsync()
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("The following aliases are available");
            foreach (var c in tagAliasesDesc)
            {
                messageBuilder.AppendLine($"\t{c}");
            }
            return ReplyAsync(messageBuilder.ToString());
        }


        [Command("db")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        [RequireNsfw(ErrorMessage = NsfwErrorMessage)]
        public async Task DanbooruSearchAsync(params string[] criteria)
        {
            await InitialCommandHandler(Danbooru, criteria);
        }

        [Command("fur")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        [RequireNsfw(ErrorMessage = NsfwErrorMessage)]
        public async Task e621SearchAsync(params string[] criteria)
        {
            await InitialCommandHandler(e621, criteria);
        }


        [Command("gb")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        [RequireNsfw(ErrorMessage = NsfwErrorMessage)]
        public async Task GelbooruSearchAsync(params string[] criteria)
        {
            await InitialCommandHandler(Gelbooru, criteria);
        }

        [Command("sb")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        [RequireNsfw(ErrorMessage = NsfwErrorMessage)]
        public async Task SafebooruSearchAsync(params string[] criteria)
        {
            await InitialCommandHandler(Safebooru, criteria);
        }

        [Command("yan")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        // [RequireNsfw(ErrorMessage = NsfwErrorMessage)]
        public async Task YandereSearchAsync(params string[] criteria)
        {
            await InitialCommandHandler(Yandere, criteria);
        }



        private async Task InitialCommandHandler<TResponse, T>(
            BooruService<TResponse, T> service,
            params string[] criteria)
        {
            var newCriteria = ExpandCriteria(criteria);
            var parameters = GetSkipAndTake(ref newCriteria);

            var pageNumber = parameters["skip"];
            var pageSize = parameters["take"];

            var results = (await service.SearchAsync(pageSize, pageNumber, newCriteria)).ToList();
            var posts = results.Select(c => Mapper.Map<T, Models.EmbedPost>(c));
            await PostAsync(posts, newCriteria, pageNumber, pageSize);
        }

        private async Task PostAsync(IEnumerable<EmbedPost> results, string[] criteria, int pageNumber, int pageSize)
        {
            var messages = new List<Embed>();
            using (var ts = Context.Channel.EnterTypingState())
            {
                if (!results.Any())
                {
                    await Context.Channel.SendMessageAsync($"uwu oopsie-woopsie you made a lil fucksy-wucksy with your inqwery sooo I have nothing to showy-wowie! (Searched using: {string.Join(", ", criteria)})");
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
                            .WithFooter($"Requested By: {Context.User.Username} | Page: {pageNumber}")
                            .WithUrl(booruPost.PageUrl);
                        messages.Add(eBuilder.Build());
                    }
                    catch (ArgumentException are)
                    {
                        Console.WriteLine(are);
                        continue;
                    }
                }
                await PaginationService.Send(Context.Channel, new BetterPaginationMessage(messages, true, Context.User, "Image") { IsNsfw = true });
            }
        }

        private string[] ExpandCriteria(string[] c)
        {
            var tags = new List<string>(c);
            var results = new List<string>();
            if (Context.Channel is Discord.ITextChannel t && !t.IsNsfw) tags.Add("-s");
            foreach (var i in tags) results.Add(tagAliases.TryGetValue(i.ToLowerInvariant(), out var alias) ? alias : i);
            return results.ToArray();
        }

        private IReadOnlyDictionary<string, int> GetSkipAndTake(ref string[] c)
        {
            var updated = new List<string>();
            var results = new Dictionary<string, int>
            {
                ["take"] = 50,
                ["skip"] = 1
            };
            for (var index = 0; index < c.Length; index++)
            {
                switch (c[index].ToLowerInvariant())
                {
                    case "--take":
                        if (int.TryParse(c[index + 1], out var t))
                        {
                            results["take"] = t;
                        }
                        index++;
                        break;
                    case "--skip":
                        if (int.TryParse(c[index + 1], out var s))
                        {
                            results["skip"] = s;
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