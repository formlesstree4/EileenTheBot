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
        
        public BetterPaginationService PaginationService { get; set; }

        public DanbooruService BooruService { get; set; }


        // [Command("e621")]
        // [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        // [RequireNsfw(ErrorMessage = "Hey. You can't post this in a non-lewd channel. Do you wanna get yelled at?")]
        // public async Task FurrySearchAsync(params string[] criteria)
        // {
            
        // }

        [Command("booru")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        [RequireNsfw(ErrorMessage = "Hey. You can't post this in a non-lewd channel. Do you wanna get yelled at?")]
        public async Task DanbooruSearchAsync(params string[] criteria)
        {
            var messages = new List<Embed>();
            var results = (await BooruService.SearchAsync(count, 1, criteria)).ToList();
            using (var ts = Context.Channel.EnterTypingState())
            {
                // await Context.Message.DeleteAsync();
                if (results.Count == 0)
                {
                    await Context.Channel.SendMessageAsync("I didn't find any good stuff. Try again.");
                    return;
                }
                foreach (var booruPost in results)
                {
                    var eBuilder = new EmbedBuilder()
                        .WithAuthor(new EmbedAuthorBuilder()
                            .WithName($"More by {booruPost.tag_string_artist}")
                            .WithIconUrl(Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl())
                            .WithUrl(booruPost.GetArtistUrl()))
                        .WithColor(new Color(152, 201, 124))
                        .WithCurrentTimestamp()
                        .WithImageUrl(booruPost.GetDownloadUrl())
                        .WithTitle($"Load Post")
                        .WithFooter($"Requested By: {Context.User.Username}")
                        .WithUrl(booruPost.GetPostUrl());
                    eBuilder.AddField("Criteria", string.Join(", ", criteria), true);
                    messages.Add(eBuilder.Build());
                }
                await PaginationService.Send(Context.Channel, new BetterPaginationMessage(messages, true, Context.User) { IsNsfw = true });
            }
        }

    }

}