using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace Bot.Services
{

    [Summary("Makes it easy to add general reactions to specified messages")]
    public sealed class ReactionHelperService : IEileenService
    {

        private readonly Emoji _disapprovalEmoji = new("👎");

        private readonly Emoji _approvalEmoji = new("👍");

        private readonly Emoji _thinkEmoji = new("🤔");


        public async Task AddMessageReaction(IMessage message, ReactionType type)
        {
            switch (type)
            {
                case ReactionType.Approval:
                    await message.AddReactionAsync(_approvalEmoji);
                    break;
                case ReactionType.Denial:
                    await message.AddReactionAsync(_disapprovalEmoji);
                    break;
                case ReactionType.Think:
                    await message.AddReactionAsync(_thinkEmoji);
                    break;
            }
        }




        public enum ReactionType
        {
            Approval,
            Denial,
            Think
        }

    }

}
