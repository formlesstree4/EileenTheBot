using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bot.Models.e621
{
    public class Relationships
    {

        [JsonProperty("parent_id")]
        public int? ParentId { get; set; }

        [JsonProperty("has_children")]
        public bool HasChildren { get; set; }

        [JsonProperty("has_active_children")]
        public bool HasActiveChildren { get; set; }

        [JsonProperty("children")]
        public IList<int> Children { get; set; }
    }
}