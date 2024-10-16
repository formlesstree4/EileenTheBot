using Bot.Models.Booru.e621;
using Discord.Commands;
using System.Collections.Generic;

namespace Bot.Services.Booru
{

    [Summary("Provides access to the e621 API in order to find images for browsing or downloading")]
    public sealed class e621 : BooruService<PostResponse, Post>, IEileenService
    {
        public e621(CredentialsService credentials) : base(credentials) { }

        public override string Name => "e621";

        protected override IEnumerable<Post> ConvertResponseAsEnumerable(PostResponse response) => response.Posts;

        protected override string GetCredentialsKey() => "e621";

        protected override string GetSearchString(int limit, int page, string searchTags) => $"https://e621.net/posts.json?limit={limit}&page={page}&tags={searchTags}";

    }

}
