using System.Threading.Tasks;
using Discord.Commands;
using Bot.Services;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;

namespace Bot.Modules
{

    public sealed class BooruModule : ModuleBase<SocketCommandContext>
    {

        private readonly int count = 50;

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

        public DanbooruService BooruService { get; set; }

        [Command("aliases")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        public Task ListTagAliasesAsync() 
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("The following aliases are available");
            foreach(var c in tagAliasesDesc)
            {
                messageBuilder.AppendLine($"\t{c}");
            }
            return ReplyAsync(messageBuilder.ToString());
        }
            

        [Command("db")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        [RequireNsfw(ErrorMessage = "Hey. You can't post this in a non-lewd channel. Do you wanna get yelled at?")]
        public async Task DanbooruSearchAsync(params string[] criteria)
        {            
            var newCriteria = ExpandCriteria(criteria);
            var parameters = GetSkipAndTake(ref newCriteria);
            var messages = new List<Embed>();

            var pageNumber = parameters["skip"];
            var pageSize = parameters["take"];

            var results = (await BooruService.SearchAsync(pageSize, pageNumber, newCriteria)).ToList();
            using (var ts = Context.Channel.EnterTypingState())
            {
                if (results.Count == 0)
                {
                    await Context.Channel.SendMessageAsync($"uwu oopsie-woopsie you made a lil fucksy-wucksy with your inqwery sooo I have nothing to showy-wowie! (Searched using: {string.Join(", ", newCriteria)})");
                    return;
                }
                foreach (var booruPost in results)
                {
                    var artistName = !string.IsNullOrWhiteSpace(booruPost.tag_string_artist) ? booruPost.tag_string_artist : "N/A";
                    var eBuilder = new EmbedBuilder()
                        .AddField("Criteria", string.Join(", ", newCriteria), true)
                        .AddField("Artist(s)", artistName, true)
                        .WithAuthor(new EmbedAuthorBuilder()
                            .WithName("Search Results")
                            .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl()))
                        .WithColor(new Color(152, 201, 124))
                        .WithCurrentTimestamp()
                        .WithImageUrl(booruPost.GetDownloadUrl())
                        .WithTitle($"The Good Stuff")
                        .WithFooter($"Requested By: {Context.User.Username} | Page: {pageNumber}")
                        .WithUrl(booruPost.GetPostUrl());
                    messages.Add(eBuilder.Build());
                }
                await PaginationService.Send(Context.Channel, new BetterPaginationMessage(messages, true, Context.User) { IsNsfw = true });
            }
        }


        private string[] ExpandCriteria(string[] c)
        {
            var results = new List<string>();
            foreach (var i in c)
            {
                results.Add(tagAliases.TryGetValue(i.ToLowerInvariant(), out var alias) ? alias : i);
            }
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
                switch(c[index].ToLowerInvariant())
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