using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bot.Models.e621
{
    public class Post
    {

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [JsonProperty("file")]
        public File File { get; set; }

        [JsonProperty("preview")]
        public Preview Preview { get; set; }

        [JsonProperty("sample")]
        public Sample Sample { get; set; }

        [JsonProperty("score")]
        public Score Score { get; set; }

        [JsonProperty("tags")]
        public Tags Tags { get; set; }

        [JsonProperty("locked_tags")]
        public IList<object> LockedTags { get; set; }

        [JsonProperty("change_seq")]
        public int ChangeSeq { get; set; }

        [JsonProperty("flags")]
        public Flags Flags { get; set; }

        [JsonProperty("rating")]
        public string Rating { get; set; }

        [JsonProperty("fav_count")]
        public int FavCount { get; set; }

        [JsonProperty("sources")]
        public IList<string> Sources { get; set; }

        [JsonProperty("pools")]
        public IList<int> Pools { get; set; }

        [JsonProperty("relationships")]
        public Relationships Relationships { get; set; }

        [JsonProperty("approver_id")]
        public int? ApproverId { get; set; }

        [JsonProperty("uploader_id")]
        public int UploaderId { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("comment_count")]
        public int CommentCount { get; set; }

        [JsonProperty("is_favorited")]
        public bool IsFavorited { get; set; }


        public string GetPostUrl()
        {
            return $"https://e621.net/posts/{Id}";
        }

    }
}