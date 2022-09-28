using Newtonsoft.Json;

namespace Bot.Models.Booru.Safebooru
{

    public sealed class Post
    {

        [JsonProperty("directory")]
        public string Directory { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; }

        [JsonProperty("change")]
        public int? Change { get; set; }

        [JsonProperty("owner")]
        public string Owner { get; set; }

        [JsonProperty("parent_id")]
        public int? ParentId { get; set; }

        [JsonProperty("rating")]
        public string Rating { get; set; }

        [JsonProperty("sample")]
        public bool? Sample { get; set; }

        [JsonProperty("sample_height")]
        public int SampleHeight { get; set; }

        [JsonProperty("sample_width")]
        public int SampleWidth { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("tags")]
        public string Tags { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }


        public string GetPageUrl() => $"https://safebooru.com/index.php?page=post&s=view&id={Id}";

        public string GetImageUrl() => $"https://safebooru.org/images/{Directory}/image";

    }

}
