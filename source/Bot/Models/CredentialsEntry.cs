using Newtonsoft.Json;

namespace Bot.Models
{

    public sealed class CredentialsEntry
    {

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("user")]
        public string Username { get; set; }

        [JsonProperty("key")]
        public string ApiKey { get; set; }

    }

}