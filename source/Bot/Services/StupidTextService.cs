using Bot.Services.RavenDB;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
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

        private readonly List<string> _statements = new();
        private readonly RavenDatabaseService _rdbs;
        private readonly Random _random;
        private readonly ILogger<StupidTextService> _logger;

        public StupidTextService(
            RavenDatabaseService rdbs,
            Random random,
            ILogger<StupidTextService> logger)
        {
            _rdbs = rdbs ?? throw new ArgumentNullException(nameof(rdbs));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        public async Task InitializeService()
        {
            _logger.LogInformation("Retrieving captions.txt from RavenDB");
            using var captionFile = await _rdbs.GetOrAddDocumentStore("erector_core").Operations.SendAsync(new GetAttachmentOperation(
                documentId: "configuration",
                name: "captions.txt",
                type: AttachmentType.Document,
                changeVector: null));
            using var reader = new StreamReader(captionFile.Stream);
            while (!reader.EndOfStream)
            {
                _statements.Add(reader.ReadLine());
            }
        }

        public string GetRandomStupidText()
        {
            lock (this)
            {
                return _statements[_random.Next(0, _statements.Count - 1)];
            }
        }
    }
}
