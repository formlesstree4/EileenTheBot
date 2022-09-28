using Newtonsoft.Json;

namespace Bot.Models.Booru.e621
{
    public class Score
    {

        [JsonProperty("up")]
        public int Up { get; set; }

        [JsonProperty("down")]
        public int Down { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }
    }
}
