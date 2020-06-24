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
            var configurationLocation = Environment.GetEnvironmentVariable("Credentials");
            if (File.Exists(configurationLocation))
            {
                LoadConfigurationFromFile(configurationLocation);
            }
            else
            {
                LoadConfigurationFromString(configurationLocation);
            }

        }


        private void LoadConfigurationFromFile(string file)
        {
            var credString = File.ReadAllText(file);
            LoadConfigurationFromString(credString);
        }

        private void LoadConfigurationFromString(string text)
        {
            var localCredentials = JsonConvert.DeserializeObject<Bot.Models.CredentialsEntry[]>(text);
            Credentials = new List<Bot.Models.CredentialsEntry>(localCredentials).AsReadOnly();
        }

    }

}