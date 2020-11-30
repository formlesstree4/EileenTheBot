using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bot.Models
{

    /// <summary>
    ///     Represents the current knowledge Eileen has about a User!
    /// </summary>
    public sealed class EileenUserData
    {

        [JsonProperty]
        public ulong UserId { get; set; }

        [JsonProperty]
        public DateTime Created { get; set; } = DateTime.Now;

        [JsonProperty]
        public List<ulong> ServersOn { get; set; } = new List<ulong>();

        [JsonProperty]
        public string ProfileImage { get; set; }

        [JsonProperty]
        internal Dictionary<string, object> _tags { get; set; } = new Dictionary<string, object>();


        public T GetTagData<T>(string tagName)
        {
            if (_tags.ContainsKey(tagName) && _tags[tagName] is T output)
            {
                return output;
            }
            throw new ArgumentException(tagName);
        }

        public T GetOrAddTagData<T>(string tagName, Func<T> addFunc)
        {
            if (!_tags.ContainsKey(tagName))
            {
                this.SetTagData(tagName, addFunc());
            }
            return this.GetTagData<T>(tagName);
        }

        public bool HasTagData(string tagName) => _tags.ContainsKey(tagName);

        public void SetTagData<T>(string tagName, T tagData)
        {
            _tags[tagName] = tagData;
        }

    }

}