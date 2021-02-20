using System.Collections.Generic;
using Bot.Models.Yandere;
using Discord.Commands;

namespace Bot.Services.Booru
{
    [Summary("Provides access to the Yande.re API in order to find images for browsing or downloading")]
    public sealed class Yandere : BooruService<Post[], Post>, IEileenService
    {
        public Yandere(CredentialsService credentials) : base(credentials) { }

        protected override IEnumerable<Post> ConvertResponseAsEnumerable(Post[] response) => response;

        protected override string GetCredentialsKey() => "yandere";

        protected override string GetSearchString(int limit, int page, string searchTags)
            => $"https://yande.re/post.json?limit={limit}&page={page}&tags={searchTags}";
    }
}