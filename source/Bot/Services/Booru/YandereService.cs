using System.Collections.Generic;
using Bot.Models.Yandere;

namespace Bot.Services.Booru
{
    public sealed class Yandere : BooruService<Post[], Post>
    {
        public Yandere(CredentialsService credentials) : base(credentials) { }

        protected override IEnumerable<Post> ConvertResponseAsEnumerable(Post[] response) => response;

        protected override string GetCredentialsKey() => "yandere";

        protected override string GetSearchString(int limit, int page, string searchTags)
            => $"https://yande.re/post.json?limit={limit}&page={page}&tags={searchTags}";
    }
}