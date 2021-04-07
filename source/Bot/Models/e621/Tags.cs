using Newtonsoft.Json;
using System.Collections.Generic;

namespace Bot.Models.e621
{
    public class Tags
    {

        [JsonProperty("general")]
        public IList<string> General { get; set; }

        [JsonProperty("species")]
        public IList<string> Species { get; set; }

        [JsonProperty("character")]
        public IList<string> Character { get; set; }

        [JsonProperty("copyright")]
        public IList<string> Copyright { get; set; }

        [JsonProperty("artist")]
        public IList<string> Artist { get; set; }

        [JsonProperty("invalid")]
        public IList<object> Invalid { get; set; }

        [JsonProperty("lore")]
        public IList<string> Lore { get; set; }

        [JsonProperty("meta")]
        public IList<string> Meta { get; set; }
    }
}