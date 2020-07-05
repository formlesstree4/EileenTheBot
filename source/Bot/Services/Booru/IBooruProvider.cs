using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.Services.Booru
{

    /// <summary>
    ///     A generic layer for interacting with various booru-like APIs, such as Danbooru, Gelbooru, and e621
    /// </summary>
    public interface IBooruProvider<T>
    {

        /// <summary>
        ///     Asynchronously performs a search and returns zero or more posts
        /// </summary>
        /// <param name="limit">The number of images that can be returned</param>
        /// <param name="page">The page offset for pagination support</param>
        /// <param name="searchTags">A string array of tags that should be searched</param>
        /// <returns>A collection of post objects (as type T)</returns>
        Task<IEnumerable<T>> SearchAsync(int limit, int page, params string[] searchTags);

    }

}