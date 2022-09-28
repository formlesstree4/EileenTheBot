using Bot.Models.Booru.Danbooru;
using Discord.Commands;
using System.Collections.Generic;

namespace Bot.Services.Booru
{



    /// <summary>
    ///     A simplified querying engine for Danbooru.
    /// </summary>
    [Summary("Provides access to the Danbooru API in order to find images for browsing or downloading")]
    public sealed class Danbooru : BooruService<Post[], Post>, IEileenService
    {
        public Danbooru(CredentialsService credentials) : base(credentials) { }

        public override string Name => "Danbooru";

        protected override IEnumerable<Post> ConvertResponseAsEnumerable(Post[] response) => response;

        protected override string GetCredentialsKey() => "danbooru";

        protected override string GetSearchString(int limit, int page, string searchTags) => $"https://danbooru.donmai.us/posts.json?limit={limit}&page={page}&tags={searchTags}";
    }


}
