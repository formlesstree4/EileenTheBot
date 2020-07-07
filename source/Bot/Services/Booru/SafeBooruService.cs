using System.Collections.Generic;
using Bot.Models.Safebooru;

namespace Bot.Services.Booru
{
    public sealed class SafeBooruService : BooruService<Post[], Post>
    {
        public SafeBooruService(CredentialsService credentials) : base(credentials) { }

        protected override IEnumerable<Post> ConvertResponseAsEnumerable(Post[] response) => response;

        protected override string GetCredentialsKey() => "safebooru";

        protected override string GetSearchString(int limit, int page, string searchTags) =>
            $"https://safebooru.org/index.php?page=dapi&s=post&q=index&pid={page}&limit={limit}&tags={searchTags}&json=1";
    }
}