using Newtonsoft.Json;

namespace Bot.Models.e621
{
    public class Sample
    {

        [JsonProperty("has")]
        public bool Has { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

}