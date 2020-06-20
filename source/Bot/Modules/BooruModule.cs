using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Bot.Services;


namespace Bot.Modules
{

    public sealed class BooruModule : ModuleBase<SocketCommandContext>
    {

        
        public BetterPaginationService PaginationService { get; set; }


        [Command("e621")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        [RequireNsfw(ErrorMessage = "Hey. You can't post this in a non-lewd channel. Do you wanna get yelled at?")]
        public Task FurrySearchAsync(params string[] criteria)
        {







        }

        [Command("booru")]
        [RequireContext(ContextType.Guild, ErrorMessage = "Hey. Public channels only.")]
        [RequireNsfw(ErrorMessage = "Hey. You can't post this in a non-lewd channel. Do you wanna get yelled at?")]
        public Task DanbooruSearchAsync(params string[] criteria)
        {

        }

    }

}