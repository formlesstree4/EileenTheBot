using System.Threading.Tasks;
using Bot.Services;
using Discord.Commands;

namespace Bot.Modules
{

    public sealed class ProfileModule : ModuleBase<SocketCommandContext>
    {

        public UserService UserService { get; set; }

        [Command("profile")]
        [Summary("Pulls up the User Profile information")]
        public async Task ProfileAsync()
        {
            var userId = Context.User.Id;
            await UserService.CreateUserProfileMessage(userId, Context.Channel);
        }

        [Command("setimage")]
        [Summary("Sets up a profile image for you on your Profile page")]
        public async Task SetProfileImageAsync([Name("Image URL"), Summary("The URL of the image to use on the Profile Page")]string imageUrl)
        {
            var userData = await UserService.GetOrCreateUserData(Context.User.Id);
            userData.ProfileImage = imageUrl;
            await Context.Channel.SendMessageAsync("Your profile image has been updated successfully");
        }

    }

}