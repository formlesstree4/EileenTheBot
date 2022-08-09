using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Bot.Models
{


    public abstract class TagEntry
    {

        [JsonProperty("_tags")]
        internal Dictionary<string, object> Tags { get; set; } = new Dictionary<string, object>();


        public T GetTagData<T>(string tagName) where T : new()
        {
            if (Tags.ContainsKey(tagName))
            {
                if (Tags[tagName] is T output) return output;
                if (Tags[tagName] is JArray)
                {
                    Tags[tagName] = new T();
                    return (T)Tags[tagName];
                }
            }
            throw new ArgumentException(tagName);
        }

        public T GetOrAddTagData<T>(string tagName, Func<T> addFunc) where T : new()
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
