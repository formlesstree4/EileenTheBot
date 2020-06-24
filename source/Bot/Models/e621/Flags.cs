using Newtonsoft.Json;

namespace Bot.Models.e621
{
    public class Flags
    {

        [JsonProperty("pending")]
        public bool Pending { get; set; }

        [JsonProperty("flagged")]
        public bool Flagged { get; set; }

        [JsonProperty("note_locked")]
        public bool NoteLocked { get; set; }

        [JsonProperty("status_locked")]
        public bool StatusLocked { get; set; }

        [JsonProperty("rating_locked")]
        public bool RatingLocked { get; set; }

        [JsonProperty("deleted")]
        public bool Deleted { get; set; }
    }
}