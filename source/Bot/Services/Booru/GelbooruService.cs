using Bot.Models.Gelbooru;
using Discord.Commands;
using System.Collections.Generic;

namespace Bot.Services.Booru
{

    [Summary("Provides access to the Gelbooru API in order to find images for browsing or downloading")]
    public sealed class Gelbooru : BooruService<Post[], Post>, IEileenService
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