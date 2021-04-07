using Bot.Services.RavenDB;
using Discord.Commands;
using Raven.Client.Documents.Attachments;
using Raven.Client.Documents.Operations.Attachments;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Bot.Services
{
    [Summary("Pulls random one-liners out a fairly large resource file stored in RavenDB")]
    public sealed class StupidTextService : IEileenService
    {

        private readonly List<string> _statements = new List<string>();
        private readonly RavenDatabaseService rdbs;
        private readonly Random random;


        public StupidTextService(
            RavenDB.RavenDatabaseService rdbs,
            Random random)
        {
            this.rdbs = rdbs ?? throw new ArgumentNullException(nameof(rdbs));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }


        public async Task InitializeService()
        {
            Console.WriteLine("Querying DB for captions...");
            using (var captionFile = await rdbs.GetOrAddDocumentStore("erector_core").Operations.SendAsync(new GetAttachmentOperation(
                documentId: "configuration",
                name: "captions.txt",
                type: AttachmentType.Document,
                changeVector: null)))
            using (var reader = new StreamReader(captionFile.Stream))
            {
                while (!reader.EndOfStream)
                {
                    _statements.Add(reader.ReadLine());
                }
            }
        }

        public string GetRandomStupidText()
        {
            lock (this)
            {
                return _statements[random.Next(0, _statements.Count - 1)];
            }
        }


    }
}