using Newtonsoft.Json;

namespace Bot.Models.Booru.e621
{
    public class Preview
    {

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
