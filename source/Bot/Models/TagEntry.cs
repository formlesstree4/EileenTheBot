using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bot.Models
{


    public abstract class TagEntry
    {

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