using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Bot.Services
{

    public sealed class CredentialsService
    {

        public IReadOnlyList<Bot.Models.CredentialsEntry> Credentials { get; private set; }


        public CredentialsService()
        {
            var configurationLocation = Environment.GetEnvironmentVariable("EILEEN_CONFIG");
            if (File.Exists(configurationLocation))
            {
                LoadConfigurationFromFile(configurationLocation);
            }

        }


        private void LoadConfigurationFromFile(string file)
        {
            var credString = File.ReadAllText(file);
            var localCredentials = JsonConvert.DeserializeObject<Bot.Models.CredentialsEntry[]>(credString);
            Credentials = new List<Bot.Models.CredentialsEntry>(localCredentials).AsReadOnly();
        }

    }

}