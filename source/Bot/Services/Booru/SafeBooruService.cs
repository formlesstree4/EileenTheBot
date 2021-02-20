using System.Collections.Generic;
using Bot.Models.Safebooru;
using Discord.Commands;

namespace Bot.Services.Booru
{
    [Summary("Provides access to the SafeBooru API in order to find images for browsing or downloading")]
    public sealed class SafeBooru : BooruService<Post[], Post>, IEileenService
    {
        public SafeBooru(CredentialsService credentials) : base(credentials) { }

        protected override IEnumerable<Post> ConvertResponseAsEnumerable(Post[] response) => response;

        protected override string GetCredentialsKey() => "safebooru";

        protected override string GetSearchString(int limit, int page, string searchTags) =>
            $"https://safebooru.org/index.php?page=dapi&s=post&q=index&pid={page}&limit={limit}&tags={searchTags}&json=1";
    }
}