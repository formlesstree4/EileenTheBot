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
        private readonly Random _random;

        public CoolSwiftModule(Random random)
        {
            _random = random;
        }


        [SlashCommand("quinn", "ᕙ(▀̿ĺ̯▀̿ ̿)ᕗ QUINnNnNnnNNN!~")]
        public async Task Quinn()
        {
            var quinnFiles = Directory.GetFiles(Path.Combine("Resources", "Quinn"));
            var fileToSend = quinnFiles[_random.Next(quinnFiles.Length - 1)];
            await using var reader = new FileStream(fileToSend, FileMode.Open, FileAccess.Read);
            var attachment = new FileAttachment(reader, Path.GetFileName(fileToSend));
            await RespondWithFileAsync(attachment, ephemeral: true);
            reader.Close();
        }


    }
}
