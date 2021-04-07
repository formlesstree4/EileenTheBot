using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace Bot.Services
{

    [Summary("Makes it easy to add general reactions to specified messages")]
    public sealed class ReactionHelperService : IEileenService
    {

        private readonly Emoji disapprovalEmoji = new Emoji("👎");

        private readonly Emoji approvalEmoji = new Emoji("👍");



        public async Task AddMessageReaction(IMessage message, ReactionType type)
        {
            switch (type)
            {
                case ReactionType.Approval:
                    await message.AddReactionAsync(approvalEmoji);
                    break;
                case ReactionType.Denial:
                    await message.AddReactionAsync(disapprovalEmoji);
                    break;
            }
        }




        public enum ReactionType
        {
            Approval,
            Denial
        }

    }

}