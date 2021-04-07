using Newtonsoft.Json;
using System.Collections.Generic;

namespace Bot.Models.e621
{
    public class PostResponse
    {

        [JsonProperty("posts")]
        public IList<Post> Posts { get; set; }
    }
}