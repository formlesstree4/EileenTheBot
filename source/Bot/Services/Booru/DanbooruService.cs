using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Bot.Models.Danbooru;
using Newtonsoft.Json;

namespace Bot.Services.Booru
{



    /// <summary>
    ///     A simplified querying engine for Danbooru.
    /// </summary>
    public sealed class DanbooruService : IBooruProvider<Post>
    {

        private readonly HttpClient _clientAsync;
        private readonly string _user;
        private readonly string _apiKey;



        /// <summary>
        ///     Creates a new instance of the <see cref="DanbooruService"/>.
        /// </summary>
        /// <param name="user">The username to login with</param>
        /// <param name="apiKey">The API key to use for validation</param>
        public DanbooruService(CredentialsService credentials)
        {
            var configuration = credentials.Credentials.First(c => c.Name.Equals("danbooru"));
            _user = configuration.Username;
            _apiKey = configuration.ApiKey;
            _clientAsync = new HttpClient();
            _clientAsync.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_user}:{_apiKey}")));
        }



        /// <summary>
        ///     Asynchronously performs a search against the Danbooru API.
        /// </summary>
        /// <param name="limit">The number of images that can be returned</param>
        /// <param name="page">The page offset for pagination support</param>
        /// <param name="searchTags">A string array of tags that should be searched</param>
        /// <returns>A collection of <see cref="Post"/> objects</returns>
        public async Task<IEnumerable<Post>> SearchAsync(int limit, int page, params string[] searchTags)
        {
            var tags = WebUtility.UrlEncode(string.Join(" ", searchTags));
            var url = $"https://danbooru.donmai.us/posts.json?limit={limit}&page={page}&tags={tags}";
            var resp = await _clientAsync.GetAsync(url);
            var respString = await resp.Content.ReadAsStringAsync();
            var posts = JsonConvert.DeserializeObject<Post[]>(respString);
            return posts;
        }

        /// <summary>
        ///     Asynchronously performs a search against the Danbooru API using the 'random' endpoint
        /// </summary>
        /// <param name="searchTags">A string array of tags that should be searched</param>
        /// <returns><see cref="Post"/></returns>
        public async Task<Post> SearchRandom(params string[] searchTags)
        {
            var tags = WebUtility.UrlEncode(string.Join(" ", searchTags));
            var url = $"https://danbooru.donmai.us/posts/random.json?tags={tags}";
            var resp = await _clientAsync.GetAsync(url);
            var respString = await resp.Content.ReadAsStringAsync();
            var post = JsonConvert.DeserializeObject<Post>(respString);
            return post;
        }

        /// <summary>
        ///     Asynchronously downloads a post.
        /// </summary>
        /// <param name="p">The <see cref="Post"/> to download</param>
        /// <returns><see cref="MemoryStream"/></returns>
        public async Task<HttpResponseMessage> DownloadPostAsync(Post p)
        {
            return await _clientAsync.GetAsync(p.GetDownloadUrl());
        }

    }

#pragma warning disable IDE1006 // Naming Styles


}