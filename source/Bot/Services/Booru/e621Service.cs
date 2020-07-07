using System.Collections.Generic;
using Bot.Models.e621;

namespace Bot.Services.Booru
{

    public sealed class e621 : BooruService<PostResponse, Post>
    {
        public e621(CredentialsService credentials) : base(credentials) { }

        protected override IEnumerable<Post> ConvertResponseAsEnumerable(PostResponse response) => response.Posts;

        protected override string GetCredentialsKey() => "e621";

        protected override string GetSearchString(int limit, int page, string searchTags) => $"https://e621.net/posts.json?limit={limit}&page={page}&tags={searchTags}";
        
    }

}