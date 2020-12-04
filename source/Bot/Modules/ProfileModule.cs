using System.Linq;
using System.Threading.Tasks;
using Bot.Services;
using Discord.Commands;

namespace Bot.Modules
{

    public sealed class ProfileModule : ModuleBase<SocketCommandContext>
    {

        public UserService UserService { get; set; }

        public ReactionHelperService ReactionHelperService { get; set; }



        [Command("profile")]
        [Summary("Pulls up the User Profile information")]
        public async Task ProfileAsync(
            [Summary("The actual command you want to execute for the Profile")]string command = null,
            [Remainder, Summary("A collection of parameters that are to be passed along for use with the given command")] string[] parameters = null
        )
        {
            switch (command?.ToLowerInvariant())
            {
                case null:
                    await UserService.CreateUserProfileMessage(Context.User, Context.Channel);
                    break;
                case "clear":
                    if ((parameters?.Length ?? 0) == 0)
                    {
                        await Context.Channel.SendMessageAsync("For the 'clear' command, please specify what you're clearing!");
                        await ReactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
                        return;
                    }
                    switch (parameters[0].ToLowerInvariant())
                    {
                        case "image":
                            await ClearProfileImageAsync();
                            break;
                        case "description":
                            await UpdateProfileDescription(null);
                            break;
                    }
                    break;
                case "set":
                    if ((parameters?.Length ?? 0) == 0)
                    {
                        await Context.Channel.SendMessageAsync("For the 'set' command, please specify what you're setting!");
                        await ReactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
                        return;
                    }
                    switch (parameters[0].ToLowerInvariant())
                    {
                        case "image":
                            await SetProfileImageAsync(parameters?.Length >= 2 ? parameters[1] : null);
                            break;
                        case "description":
                            await UpdateProfileDescription(string.Join(' ', parameters.Skip(1)));
                            break;
                    }
                    break;
            }
        }

        private async Task SetProfileImageAsync(string imageUrl = null)
        {
            var userData = await UserService.GetOrCreateUserData(Context.User);
            if (string.IsNullOrWhiteSpace(imageUrl) && (Context.Message.Attachments.Count == 0 || Context.Message.Attachments.First().Height == null))
            {
                await ReactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
                return;            
            }
            userData.ProfileImage = imageUrl ?? Context.Message.Attachments.First().Url;
            await ReactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
        }

        private async Task ClearProfileImageAsync()
        {
            var userData = await UserService.GetOrCreateUserData(Context.User);
            userData.ProfileImage = "";
            await ReactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
        }

        private async Task UpdateProfileDescription(string desc)
        {
            var userData = await UserService.GetOrCreateUserData(Context.User);
            userData.Description = desc;
            await ReactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
        }

    }

}