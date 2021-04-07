using Newtonsoft.Json;
using System;
using System.IO;

namespace Bot.Models.Danbooru
{
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
        public int? UploaderId { get; set; }

        /// <summary>
        ///     Gets or sets the Score of this <see cref="Post"/>
        /// </summary>
        [JsonProperty("score")]
        public int? Score { get; set; }

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
        public int? image_width { get; set; }
        public int? image_height { get; set; }
        public string tag_string { get; set; }
        public bool? is_note_locked { get; set; }
        public int? fav_count { get; set; }
        public string file_ext { get; set; }
        public DateTime? last_noted_at { get; set; }
        public bool? is_rating_locked { get; set; }
        public int? parent_id { get; set; }
        public bool? has_children { get; set; }
        public int? approver_id { get; set; }
        public int? tag_count_general { get; set; }
        public int? tag_count_artist { get; set; }
        public int? tag_count_character { get; set; }
        public int? tag_count_copyright { get; set; }
        public int? file_size { get; set; }
        public bool? is_status_locked { get; set; }
        public string fav_string { get; set; }
        public string pool_string { get; set; }
        public int? up_score { get; set; }
        public int? down_score { get; set; }
        public bool? is_pending { get; set; }
        public bool? is_flagged { get; set; }
        public bool? is_deleted { get; set; }
        public int? tag_count { get; set; }
        public DateTime? updated_at { get; set; }
        public bool? is_banned { get; set; }
        public int? pixiv_id { get; set; }
        public DateTime? last_commented_at { get; set; }
        public bool? has_active_children { get; set; }
        public int? bit_flags { get; set; }
        public string uploader_name { get; set; }
        public bool? has_large { get; set; }
        public string tag_string_artist { get; set; }
        public string tag_string_character { get; set; }
        public string tag_string_copyright { get; set; }
        public string tag_string_general { get; set; }
        public bool? has_visible_children { get; set; }
        public string file_url { get; set; }
        public string large_file_url { get; set; }
        public string preview_file_url { get; set; }

        public string GetPostUrl() => $"https://danbooru.donmai.us/posts/{ID}";
        public string GetDownloadUrl() => $"{file_url ?? large_file_url}";
        public string GetRenderName() => Path.GetFileNameWithoutExtension(preview_file_url.Substring(preview_file_url.LastIndexOf('/') + 1));
        public string GetArtistUrl()
        {
            switch (tag_count_artist)
            {
                case 0: return "";
                default:
                    var artists = tag_string_artist.Split(',');
                    return $"https://danbooru.donmai.us/posts?tags={artists[0]}";
            }
        }


    }

}