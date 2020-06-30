using System.Threading.Tasks;
using Discord.Commands;
using Bot.Services;
using System.Linq;
using System.Collections.Generic;
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
            ["-s"] = "rating:safe",
        };

        public BetterPaginationService PaginationService { get; set; }

        public DanbooruService BooruService { get; set; }

        [Command("aliases")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        public Task ListTagAliasesAsync() =>
            ReplyAsync(string.Join("|", tagAliases.ToList()));

        [Command("db")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        [RequireNsfw(ErrorMessage = "Hey. You can't post this in a non-lewd channel. Do you wanna get yelled at?")]
        public async Task DanbooruSearchAsync(params string[] criteria)
        {
            string[] ExpandCriteria(string[] c)
            {
                var results = new List<string>();
                foreach (var i in c)
                {
                    if (tagAliases.TryGetValue(i.ToLowerInvariant(), out var alias))
                        results.Add(alias);
                    else
                        results.Add(i);
                }
                return results.ToArray();
            }
            
            var newCriteria = ExpandCriteria(criteria);
            var messages = new List<Embed>();
            var results = (await BooruService.SearchAsync(count, 1, newCriteria)).ToList();
            using (var ts = Context.Channel.EnterTypingState())
            {
                if (results.Count == 0)
                {
                    await Context.Channel.SendMessageAsync("I didn't find any good stuff. Try again.");
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
                        .WithTitle($"Load Post")
                        .WithFooter($"Requested By: {Context.User.Username}")
                        .WithUrl(booruPost.GetPostUrl());
                    messages.Add(eBuilder.Build());
                }
                await PaginationService.Send(Context.Channel, new BetterPaginationMessage(messages, true, Context.User) { IsNsfw = true });
            }
        }

    }

}