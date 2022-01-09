using Bot.Services;
using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Modules
{

    public sealed class ProfileModule : ModuleBase<SocketCommandContext>
    {
        private readonly UserService userService;
        private readonly ReactionHelperService reactionHelperService;

        public ProfileModule(
            UserService userService,
            ReactionHelperService reactionHelperService)
        {
            this.userService = userService ?? throw new System.ArgumentNullException(nameof(userService));
            this.reactionHelperService = reactionHelperService ?? throw new System.ArgumentNullException(nameof(reactionHelperService));
        }


        [Command("profile")]
        [Summary("Pulls up the User Profile information")]
        public async Task ProfileAsync(
            [Summary("The actual command you want to execute for the Profile")] string command = null,
            [Remainder, Summary("A collection of parameters that are to be passed along for use with the given command")] string[] parameters = null
        )
        {
            switch (command?.ToLowerInvariant())
            {
                case null:
                    await userService.CreateUserProfileMessage(Context.User, Context.Channel);
                    break;
                case "clear":
                    if ((parameters?.Length ?? 0) == 0)
                    {
                        await ReplyAsync("For the 'clear' command, please specify what you're clearing!");
                        await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
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
                        await ReplyAsync("For the 'set' command, please specify what you're setting!");
                        await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
                        return;
                    }
                    switch (parameters[0].ToLowerInvariant())
                    {
                        case "image":
                            await SetProfileImageAsync(parameters?.Length >= 2 ? parameters[1] : null);
                            break;
                        case "description":
                            var description = parameters.Skip(1).ToList();
                            if (description.Count == 0)
                            {
                                await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
                                return;
                            }
                            await UpdateProfileDescription(string.Join(' ', description));
                            break;
                    }
                    break;
            }
        }

        private async Task SetProfileImageAsync(string imageUrl = null)
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            if (string.IsNullOrWhiteSpace(imageUrl) && (Context.Message.Attachments.Count == 0 || Context.Message.Attachments.First().Height == null))
            {
                await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Denial);
                return;
            }
            userData.ProfileImage = imageUrl ?? Context.Message.Attachments.First().Url;
            await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
        }

        private async Task ClearProfileImageAsync()
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            userData.ProfileImage = "";
            await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
        }

        private async Task UpdateProfileDescription(string desc)
        {
            var userData = await userService.GetOrCreateUserData(Context.User);
            userData.Description = desc;
            await reactionHelperService.AddMessageReaction(Context.Message, ReactionHelperService.ReactionType.Approval);
        }

    }

}
