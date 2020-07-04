using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Bot.Models.e621;
using Newtonsoft.Json;

namespace Bot.Services.Booru
{

    /// <summary>
    ///     A simplified querying engine for e621.
    /// </summary>
    public sealed class e621Service : IBooruProvider<Post>
    {

        private readonly HttpClient _clientAsync;
        private readonly string _user;
        private readonly string _apiKey;



        /// <summary>
        ///     Creates a new instance of the <see cref="e621Service"/>.
        /// </summary>
        public e621Service(CredentialsService credentials)
        {
            var configuration = credentials.Credentials.First(c => c.Name.Equals("e621"));
            var userAgent = new ProductHeaderValue($"The-Erector-by-{configuration.Username}", "1.0");
            _user = configuration.Username;
            _apiKey = configuration.ApiKey;
            _clientAsync = new HttpClient();
            _clientAsync.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _clientAsync.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(userAgent));
            _clientAsync.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_user}:{_apiKey}")));
        }

        public async Task<IEnumerable<Post>> SearchAsync(int limit, int page, params string[] searchTags)
        {
            var tags = WebUtility.UrlEncode(string.Join(" ", searchTags));
            var url = $"https://e621.net/posts.json?limit={limit}&page={page}&tags={tags}";
            var resp = await _clientAsync.GetAsync(url);
            var respString = await resp.Content.ReadAsStringAsync();
            Console.WriteLine(respString);
            var posts = JsonConvert.DeserializeObject<PostResponse>(respString);
            return posts.Posts;
        }

        public async Task<Post> SearchRandom(params string[] searchTags)
        {
            var tags = new List<string>(searchTags);
            tags.Add("order:random");
            return (await SearchAsync(1, 1, tags.ToArray())).FirstOrDefault();
        }
    }

}