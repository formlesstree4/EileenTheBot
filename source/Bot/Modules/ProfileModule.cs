using Bot.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Modules
{

    [Group("profile", "Interact with the Profile that Erector has about you!")]
    public sealed class ProfileModule : InteractionModuleBase
    {
        private readonly UserService userService;
        private readonly InteractionHandlingService interactionHandlingService;

        public ProfileModule(
            UserService userService,
            InteractionHandlingService modalHandlingService)
        {
            this.userService = userService ?? throw new System.ArgumentNullException(nameof(userService));
            this.interactionHandlingService = modalHandlingService ?? throw new System.ArgumentNullException(nameof(modalHandlingService));
            this.interactionHandlingService.RegisterCallbackHandler("edit-profile", new InteractionModalCallbackProvider(HandleEditProfileModal));
        }





        //[Group("clear", "Clear settable preferences on your Profile")]
        //public sealed class ClearProfileCommands : InteractionModuleBase
        //{
        //    private readonly UserService userService;

        //    public ClearProfileCommands(UserService userService)
        //    {
        //        this.userService = userService;
        //    }

        //    [SlashCommand("image", "Clear out the profile picture")]
        //    public async Task ClearPicture()
        //    {
        //        await ClearProfileImageAsync();
        //        await RespondAsync("Your profile image has been cleared!", ephemeral: true);
        //    }

        //    [SlashCommand("description", "Clear out the description")]
        //    public async Task ClearDescription()
        //    {
        //        await UpdateProfileDescription("");
        //        await RespondAsync("Your custom description has been cleared!", ephemeral: true);
        //    }

        //    private async Task ClearProfileImageAsync()
        //    {
        //        var userData = await userService.GetOrCreateUserData(Context.User);
        //        userData.ProfileImage = "";
        //    }

        //    private async Task UpdateProfileDescription(string desc)
        //    {
        //        var userData = await userService.GetOrCreateUserData(Context.User);
        //        userData.Description = desc;
        //    }

        //}


        [SlashCommand("view", "View a User's profile")]
        public async Task ViewProfileAsync(IUser user = null)
        {
            await userService.CreateUserProfileMessage(user ?? Context.User, Context);
        }

        [SlashCommand("edit", "Opens up a modal to edit your profile")]
        public async Task EditProfileAsync()
        {
            var userProfile = await userService.GetOrCreateUserData(Context.User);
            var builder = new ModalBuilder()
                .WithTitle("Profile")
                .WithCustomId("edit-profile")
                .AddTextInput("Profile Picture", "profile-url",
                    placeholder: "A link to your Erector specific pfp", value: userProfile.ProfileImage)
                .AddTextInput("Description", "profile-description", TextInputStyle.Paragraph, "A short blurb about yourself", value: userProfile.Description);
            await RespondWithModalAsync(builder.Build());
        }


        private async Task HandleEditProfileModal(SocketModal modal)
        {
            var responses = modal.Data.Components.ToList();
            var userProfile = await userService.GetOrCreateUserData(modal.User);
            userProfile.ProfileImage = responses.First(c => c.CustomId == "profile-url").Value;
            userProfile.Description = responses.First(c => c.CustomId == "profile-description").Value;
            await modal.RespondAsync("Your profile has been updated!", ephemeral: true);
        }

    }

}
