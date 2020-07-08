using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bot.Models.Yandere
{
    
    public class FlagDetail
    {

        [JsonProperty("post_id")]
        public int post_id { get; set; }

        [JsonProperty("reason")]
        public string reason { get; set; }

        [JsonProperty("created_at")]
        public DateTime created_at { get; set; }

        [JsonProperty("user_id")]
        public int? user_id { get; set; }

        [JsonProperty("flagged_by")]
        public string flagged_by { get; set; }
    }

    public class Post
    {

        [JsonProperty("id")]
        public int id { get; set; }

        [JsonProperty("tags")]
        public string tags { get; set; }

        [JsonProperty("created_at")]
        public int created_at { get; set; }

        [JsonProperty("updated_at")]
        public int? updated_at { get; set; }

        [JsonProperty("creator_id")]
        public int creator_id { get; set; }

        [JsonProperty("approver_id")]
        public int? approver_id { get; set; }

        [JsonProperty("author")]
        public string author { get; set; }

        [JsonProperty("change")]
        public int change { get; set; }

        [JsonProperty("source")]
        public string source { get; set; }

        [JsonProperty("score")]
        public int score { get; set; }

        [JsonProperty("md5")]
        public string md5 { get; set; }

        [JsonProperty("file_size")]
        public int file_size { get; set; }

        [JsonProperty("file_ext")]
        public string file_ext { get; set; }

        [JsonProperty("file_url")]
        public string file_url { get; set; }

        [JsonProperty("is_shown_in_index")]
        public bool is_shown_in_index { get; set; }

        [JsonProperty("preview_url")]
        public string preview_url { get; set; }

        [JsonProperty("preview_width")]
        public int preview_width { get; set; }

        [JsonProperty("preview_height")]
        public int preview_height { get; set; }

        [JsonProperty("actual_preview_width")]
        public int actual_preview_width { get; set; }

        [JsonProperty("actual_preview_height")]
        public int actual_preview_height { get; set; }

        [JsonProperty("sample_url")]
        public string sample_url { get; set; }

        [JsonProperty("sample_width")]
        public int sample_width { get; set; }

        [JsonProperty("sample_height")]
        public int sample_height { get; set; }

        [JsonProperty("sample_file_size")]
        public int sample_file_size { get; set; }

        [JsonProperty("jpeg_url")]
        public string jpeg_url { get; set; }

        [JsonProperty("jpeg_width")]
        public int jpeg_width { get; set; }

        [JsonProperty("jpeg_height")]
        public int jpeg_height { get; set; }

        [JsonProperty("jpeg_file_size")]
        public int jpeg_file_size { get; set; }

        [JsonProperty("rating")]
        public string rating { get; set; }

        [JsonProperty("is_rating_locked")]
        public bool is_rating_locked { get; set; }

        [JsonProperty("has_children")]
        public bool has_children { get; set; }

        [JsonProperty("parent_id")]
        public int? parent_id { get; set; }

        [JsonProperty("status")]
        public string status { get; set; }

        [JsonProperty("is_pending")]
        public bool is_pending { get; set; }

        [JsonProperty("width")]
        public int width { get; set; }

        [JsonProperty("height")]
        public int height { get; set; }

        [JsonProperty("is_held")]
        public bool is_held { get; set; }

        [JsonProperty("frames_pending_string")]
        public string frames_pending_string { get; set; }

        [JsonProperty("frames_pending")]
        public IList<object> frames_pending { get; set; }

        [JsonProperty("frames_string")]
        public string frames_string { get; set; }

        [JsonProperty("frames")]
        public IList<object> frames { get; set; }

        [JsonProperty("is_note_locked")]
        public bool is_note_locked { get; set; }

        [JsonProperty("last_noted_at")]
        public int last_noted_at { get; set; }

        [JsonProperty("last_commented_at")]
        public int last_commented_at { get; set; }

        [JsonProperty("flag_detail")]
        public FlagDetail flag_detail { get; set; }

    }

}