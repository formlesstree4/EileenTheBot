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

        private readonly int count = 5;
        
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
        public async Task DanbooruSearchAsync(int page, params string[] criteria)
        {
            var messages = new List<Embed>();
            var results = (await BooruService.SearchAsync(count, 1, criteria)).ToList();
            using (var ts = Context.Channel.EnterTypingState())
            {
                foreach (var booruPost in results)
                {
                    var eBuilder = new EmbedBuilder()
                        .WithAuthor(new EmbedAuthorBuilder()
                            .WithName($"Created By: {booruPost.tag_string_artist}")
                            .WithIconUrl(Context.User.GetAvatarUrl())
                            .WithUrl(booruPost.GetArtistUrl()))
                        .WithColor(new Color(152, 201, 124))
                        .WithCurrentTimestamp()
                        .WithDescription($"Uploaded by: {booruPost.uploader_name}")
                        .WithImageUrl(booruPost.GetDownloadUrl)
                        .WithTitle($"Artists(s): {booruPost.tag_string_artist})")
                        .WithUrl(booruPost.GetPostUrl);
                    eBuilder.AddField("All Tags", $"`{booruPost.tag_string.Replace("`", @"\`")}`", true);
                    messages.Add(eBuilder.Build());
                }
                await Context.Message.DeleteAsync();
                await PaginationService.Send(Context.Channel, new BetterPaginationMessage(messages, true, Context.User) { IsNsfw = true });
            }
        }

    }

}