using Newtonsoft.Json;
using System.Collections.Generic;

namespace Bot.Models.Booru.e621
{
    public class PostResponse
    {

        [JsonProperty("posts")]
        public IList<Post> Posts { get; set; }
    }
}
