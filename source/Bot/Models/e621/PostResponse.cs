using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bot.Models.e621
{
    public class PostResponse
    {

        [JsonProperty("posts")]
        public IList<Post> Posts { get; set; }
    }
}