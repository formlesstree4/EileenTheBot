using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Bot.Models
{


    public abstract class TagEntry
    {

        [JsonProperty("_tags")]
        internal Dictionary<string, object> Tags { get; set; } = new Dictionary<string, object>();


        public T GetTagData<T>(string tagName)
        {
            if (Tags.ContainsKey(tagName) && Tags[tagName] is T output)
            {
                return output;
            }
            throw new ArgumentException(tagName);
        }

        public T GetOrAddTagData<T>(string tagName, Func<T> addFunc)
        {
            if (!Tags.ContainsKey(tagName))
            {
                this.SetTagData(tagName, addFunc());
            }
            return this.GetTagData<T>(tagName);
        }

        public bool HasTagData(string tagName) => Tags.ContainsKey(tagName);

        public void SetTagData<T>(string tagName, T tagData)
        {
            Tags[tagName] = tagData;
        }

    }


}
