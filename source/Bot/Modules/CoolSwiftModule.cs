using Bot.Preconditions;
using Discord;
using Discord.Interactions;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Bot.Modules
{

    [Group("cs", "( ͡° ᴥ ͡°)"), CoolSwiftPrecondition]
    public sealed class CoolSwiftModule : InteractionModuleBase
    {
        private readonly Random random;

        public CoolSwiftModule(Random random)
        {
            this.random = random;
        }


        [SlashCommand("quinn", "ᕙ(▀̿ĺ̯▀̿ ̿)ᕗ QUINnNnNnnNNN!~")]
        public async Task Quinn()
        {
            var quinnFiles = Directory.GetFiles(Path.Combine("Resources", "Quinn"));
            var fileToSend = quinnFiles[random.Next(quinnFiles.Length - 1)];
            using var reader = new FileStream(fileToSend, FileMode.Open, FileAccess.Read);
            var attachment = new FileAttachment(reader, Path.GetFileName(fileToSend));
            await RespondWithFileAsync(attachment, ephemeral: true);
            reader.Close();
        }


    }
}
