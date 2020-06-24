using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Bot.Services
{



    /// <summary>
    ///     A simplified querying engine for Danbooru.
    /// </summary>
    public sealed class DanbooruService
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

    /// <summary>
    ///     The object representation of a Danbooru Image Post.
    /// </summary>
    public sealed class Post
    {

        /// <summary>
        ///     Gets or sets the unique Post ID on Danbooru.
        /// </summary>
        [JsonProperty("id")]
        public int ID { get; set; }

        /// <summary>
        ///     Gets or sets the UTC <see cref="DateTime"/> this <see cref="Post"/> was created
        /// </summary>
        [JsonProperty("created_at")]
        public DateTime Created { get; set; }

        /// <summary>
        ///     Gets or sets the User ID that uploaded this <see cref="Post"/>
        /// </summary>
        [JsonProperty("uploader_id")]
        public int UploaderId { get; set; }

        /// <summary>
        ///     Gets or sets the Score of this <see cref="Post"/>
        /// </summary>
        [JsonProperty("score")]
        public int Score { get; set; }

        /// <summary>
        ///     Gets or sets the source of this <see cref="Post"/>
        /// </summary>
        [JsonProperty("source")]
        public string source { get; set; }

        /// <summary>
        ///     Gets or sets the MD5 hash.
        /// </summary>
        [JsonProperty("md5")]
        public string MD5 { get; set; }

        /// <summary>
        ///     Gets or sets the last <see cref="DateTime"/> a comment was bumped.
        /// </summary>
        [JsonProperty("last_comment_bumped_at")]
        public DateTime? LastCommentBumped { get; set; }


        public string rating { get; set; }
        public int image_width { get; set; }
        public int image_height { get; set; }
        public string tag_string { get; set; }
        public bool is_note_locked { get; set; }
        public int fav_count { get; set; }
        public string file_ext { get; set; }
        public DateTime? last_noted_at { get; set; }
        public bool is_rating_locked { get; set; }
        public int? parent_id { get; set; }
        public bool has_children { get; set; }
        public int? approver_id { get; set; }
        public int tag_count_general { get; set; }
        public int tag_count_artist { get; set; }
        public int tag_count_character { get; set; }
        public int tag_count_copyright { get; set; }
        public int file_size { get; set; }
        public bool is_status_locked { get; set; }
        public string fav_string { get; set; }
        public string pool_string { get; set; }
        public int up_score { get; set; }
        public int down_score { get; set; }
        public bool is_pending { get; set; }
        public bool is_flagged { get; set; }
        public bool is_deleted { get; set; }
        public int tag_count { get; set; }
        public DateTime updated_at { get; set; }
        public bool is_banned { get; set; }
        public int? pixiv_id { get; set; }
        public DateTime? last_commented_at { get; set; }
        public bool has_active_children { get; set; }
        public int bit_flags { get; set; }
        public string uploader_name { get; set; }
        public bool has_large { get; set; }
        public string tag_string_artist { get; set; }
        public string tag_string_character { get; set; }
        public string tag_string_copyright { get; set; }
        public string tag_string_general { get; set; }
        public bool has_visible_children { get; set; }
        public string file_url { get; set; }
        public string large_file_url { get; set; }
        public string preview_file_url { get; set; }

        public string GetPostUrl() => $"https://danbooru.donmai.us/posts/{ID}";
        public string GetDownloadUrl() => $"{file_url ?? large_file_url}";
        public string GetRenderName() => Path.GetFileNameWithoutExtension(preview_file_url.Substring(preview_file_url.LastIndexOf('/') + 1));
        public string GetArtistUrl()
        {
            switch(tag_count_artist)
            {
                case 0: return "";
                default: return $"https://danbooru.donmai.us/posts.json?page=1&tags={tag_string_artist}";
            }
        }


    }




}