using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.Models.Gelbooru;

namespace Bot.Services.Booru
{

    public class GelbooruService : IBooruProvider<Post>
    {
        public async Task<IEnumerable<Post>> SearchAsync(int limit, int page, params string[] searchTags)
        {
            throw new System.NotImplementedException();
        }

        public async Task<Post> SearchRandom(params string[] searchTags)
        {
            throw new System.NotImplementedException();
        }
    }

}