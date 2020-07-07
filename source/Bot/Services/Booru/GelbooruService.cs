using System.Collections.Generic;
using Bot.Models.Gelbooru;

namespace Bot.Services.Booru
{

    public sealed class Gelbooru : BooruService<Post[], Post>
    {
        public Gelbooru(CredentialsService credentials) : base(credentials) { }

        protected override IEnumerable<Post> ConvertResponseAsEnumerable(Post[] response) => response;

        protected override string GetCredentialsKey() => "gelbooru";

        protected override string GetSearchString(int limit, int page, string searchTags)
        {
            var c = GetCredentials();
            return $"https://gelbooru.com/index.php?page=dapi&s=post&q=index&pid={page}&limit={limit}&tags={searchTags}&json=1&api_key={c.ApiKey}&user_id={c.Username}";
        }
    }

}