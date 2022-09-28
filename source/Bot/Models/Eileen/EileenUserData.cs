using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Bot.Models.Eileen
{

    /// <summary>
    ///     Represents the current knowledge Eileen has about a User!
    /// </summary>
    public sealed class EileenUserData : TagEntry
    {

        [JsonProperty]
        public ulong UserId { get; set; }

        [JsonProperty]
        public string Description { get; set; }

        [JsonProperty]
        public DateTime Created { get; set; } = DateTime.Now;

        [JsonProperty]
        public List<ulong> ServersOn { get; set; } = new List<ulong>();

        [JsonProperty]
        public string ProfileImage { get; set; }

    }

}
