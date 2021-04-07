using Bot.Services.RavenDB;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Services
{

    public sealed class CredentialsService : IEileenService
    {

        public IReadOnlyList<Bot.Models.CredentialsEntry> Credentials { get; private set; }


        public CredentialsService(RavenDatabaseService rdbs)
        {
            Credentials = rdbs.Configuration.Credentials.ToList();
        }

    }

}