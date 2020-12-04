using System.Linq;
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
        public async Task ProfileAsync(
            [Summary("The actual command you want to execute for the Profile")]string command = null,
            [Remainder, Summary("A collection of parameters that are to be passed along for use with the given command")] string[] parameters = null
        )
        {
            var userId = Context.User.Id;
            switch (command)
            {
                case null:
                    await UserService.CreateUserProfileMessage(userId, Context.Channel);
                    break;
                case "setimg":
                    var imageUrl = "";
                    if (parameters?.Length >= 1) imageUrl = parameters[0];
                    await SetProfileImageAsync(imageUrl);
                    break;
                case "rmimg":
                    await ClearProfileImageAsync();
                    break;
            }
        }

        private async Task SetProfileImageAsync(string imageUrl = null)
        {
            var userData = await UserService.GetOrCreateUserData(Context.User.Id);
            var fromAttachment = false;
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                foreach(var attachment in Context.Message.Attachments)
                {
                    if (attachment.Height is null) continue;    
                    imageUrl = attachment.Url;
                    fromAttachment = true;
                }
            }
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                await Context.Channel.SendMessageAsync("Please either supply an image URL OR attach an image!");
                return;
            }
            var message = "Your profile image has been updated successfully";
            if (fromAttachment)
            {
                message += " from the attachment";
            }
            userData.ProfileImage = imageUrl;
            await Context.Channel.SendMessageAsync(message);
        }


        private async Task ClearProfileImageAsync()
        {
            var userData = await UserService.GetOrCreateUserData(Context.User.Id);
            userData.ProfileImage = "";
            await Context.Channel.SendMessageAsync("Your profile image has been updated successfully");
        }

    }

}