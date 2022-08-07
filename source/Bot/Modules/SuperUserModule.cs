using Bot.Preconditions;
using Bot.Services;
using Bot.Services.Communication.Responders;
using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Modules
{

    [Group("admin", "Administrative Commands for Erector"), TrustedUsersPrecondition]
    public class SuperUserModule : InteractionModuleBase
    {
        private readonly ChannelCommunicationService channelCommunicationService;
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly CancellationTokenSource cancellationTokenSource;

        public SuperUserModule(
            ChannelCommunicationService channelCommunicationService,
            ServerConfigurationService serverConfigurationService,
            CancellationTokenSource cancellationTokenSource)
        {
            this.channelCommunicationService = channelCommunicationService;
            this.serverConfigurationService = serverConfigurationService ?? throw new ArgumentNullException(nameof(serverConfigurationService));
            this.cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));
        }

        [SlashCommand("chat", "Sets the Chat mode"), RequireContext(ContextType.Guild)]
        public async Task SetResponderAsync([Summary("type", "The type of chat responder to use"), Autocomplete(typeof(ChatAutoCompleteHandler))] string type)
        {
            var scs = await serverConfigurationService.GetOrCreateConfigurationAsync(Context.Guild);
            switch (type.ToLowerInvariant())
            {
                case "gpt":
                    scs.ResponderType = Models.ServerConfigurationData.AutomatedResponseType.GPT;
                    await serverConfigurationService.SaveServiceAsync();
                    await RespondAsync($"This Guild is now using {scs.ResponderType}");
                    break;
                case "markov":
                    scs.ResponderType = Models.ServerConfigurationData.AutomatedResponseType.Markov;
                    await serverConfigurationService.SaveServiceAsync();
                    await RespondAsync($"This Guild is now using {scs.ResponderType}");
                    break;
                default:
                    await RespondAsync($"This Guild is currently using {scs.ResponderType}");
                    break;
            }
        }

        [SlashCommand("shutdown", "Turns off the Bot")]
        public async Task KillAsync()
        {
            await RespondAsync("Initiating shutdown request...", ephemeral: true);
            cancellationTokenSource.Cancel();
        }


        [SlashCommand("history", "Builds a TXT file of the specified number of messages and responds to the User with them", runMode: RunMode.Async)]
        public async Task DownloadChannelHistory([Summary("Messages", "The number of messages to include")]int limit = 1000)
        {
            await DeleteOriginalResponseAsync();
            var channel = Context.Channel;
            var messages = await channel.GetMessagesAsync(limit).FlattenAsync();
            var messageStrings = messages
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => $"{m.Author.Username}: {m.Content}");
            var fileLocation = Path.GetTempFileName();
            var fileLocationAsText = Path.ChangeExtension(fileLocation, "txt");
            File.Move(fileLocation, fileLocationAsText);
            using (var writer = new StreamWriter(fileLocationAsText))
            {
                foreach (var message in messageStrings)
                {
                    await writer.WriteLineAsync(message);
                }
            }
            await RespondWithFileAsync(new FileAttachment(new FileStream(fileLocationAsText, FileMode.Open, FileAccess.Read), $"Messages For {Context.Channel.Name}.txt"), ephemeral: true);
            File.Delete(fileLocationAsText);
        }


        /// <summary>
        /// Sub-module for handling Service interactions
        /// </summary>
        [Group("services", "Commands for interrogating Erector's services")]
        public sealed class ServiceModule : InteractionModuleBase
        {
            private readonly ServiceManager serviceManager;

            public ServiceModule(ServiceManager serviceManager)
            {
                this.serviceManager = serviceManager;
            }


            [SlashCommand("save", "Forcibly saves any given collection of services")]
            public async Task SaveService([Summary("names", "The collection of services, separated by spaces")] params string[] names)
            {
                var services = new List<IEileenService>(GetQualifyingServices(names));
                await ReplyAsync("Saving services, please wait...");
                foreach (var service in services)
                {
                    await service.SaveServiceAsync();
                }
                await RespondAsync("All services have been saved!");
            }

            [SlashCommand("load", "Forcibly loads any given collection of services")]
            public async Task LoadService([Summary("names", "The collection of services, separated by spaces")] params string[] names)
            {
                var services = new List<IEileenService>(GetQualifyingServices(names));
                await RespondAsync("Reloading services, please wait...");
                foreach (var service in services)
                {
                    await service.LoadServiceAsync();
                }
                await RespondAsync("All services have been reloaded!");
            }

            [SlashCommand("list", "Lists all the currently running services in Eileen")]
            public async Task ListAsync()
            {
                var services = serviceManager.GetServiceNames();
                await RespondAsync($"The following services are currently running: {string.Join(", ", services)}");
            }

            private IEnumerable<IEileenService> GetQualifyingServices(string[] names)
            {
                if ((names?.Length ?? 0) == 0)
                {
                    return serviceManager.GetServices();
                }
                else
                {
                    return (from name in names
                            let service = serviceManager.GetServiceByName(name)
                            where service != null
                            select service);
                }
            }

        }

        /// <summary>
        /// Sub-module for handling macros
        /// </summary>
        [Group("macros", "Interacting with Erector's macro system"), RequireContext(ContextType.Guild)]
        public sealed class MacroModule : InteractionModuleBase
        {
            private readonly MacroService macroService;

            public MacroModule(MacroService macroService)
            {
                this.macroService = macroService ?? throw new ArgumentNullException(nameof(macroService));
            }

            [SlashCommand("list", "Lists all available macros for the server")]
            public async Task ListMacros()
            {
                var macros = await macroService.GetServerMacros(Context.Guild);
                var responseBuilder = new System.Text.StringBuilder();
                responseBuilder.AppendLine("Available Macros:");
                foreach(var macro in macros)
                {
                    responseBuilder.AppendLine($"\t{macro.Macro}");
                }
                await RespondAsync(responseBuilder.ToString(), ephemeral: true);
            }

            [SlashCommand("add", "Adds a new macro")]
            public async Task AddMacro([Summary("name", "The name of the new macro")] string macroName, [Summary("response", "The contents of the macro")]string response)
            {
                await macroService.AddNewMacroAsync(Context.Guild, new Models.Macros.MacroEntry
                {
                    Macro = macroName,
                    Response = response
                });
                await RespondAsync($"Macro {macroName} has been created", ephemeral: true);
            }

            [SlashCommand("remove", "Removes a macro")]
            public async Task RemoveMacro([Summary("name", "The name of the new macro"), Autocomplete(typeof(MacroAutocompleteHandler))] string macroName)
            {
                await macroService.RemoveMacroAsync(Context.Guild, macroName);
                await RespondAsync($"Macro {macroName} has been removed", ephemeral: true);
            }

        }

        /// <summary>
        /// Sub-module for markov stuff
        /// </summary>
        [Group("markov", "Simple management of the markov system")]
        public sealed class MarkovAdminModule : InteractionModuleBase
        {
            private readonly MarkovResponder markovResponder;

            public MarkovAdminModule(
                MarkovResponder markovResponder)
            {
                this.markovResponder = markovResponder ?? throw new ArgumentNullException(nameof(markovResponder));
            }

            [SlashCommand("train", "Trains a new Markov Chain", runMode: RunMode.Async), RequireContext(ContextType.Guild)]
            public async Task TrainChain(int length = 100)
            {
                if (!markovResponder.TryGetServerInstance(Context.Guild.Id, out var chain))
                {
                    await RespondAsync("Unable to find the correct guild context; failed ot train", ephemeral: true);
                    return;
                }

                await RespondAsync($"Training has begun; using {length} historical message(s), this could take time...", ephemeral: true);
                var channel = Context.Channel;
                var messages = await channel.GetMessagesAsync(length).FlattenAsync();
                var messageStrings = messages.Select(m => m.CleanContent);
                chain.RetrainFromScratch(messageStrings);
                await DeleteOriginalResponseAsync();
                await RespondAsync("Model has finished training", ephemeral: true);
            }

        }

        /// <summary>
        /// Sub-module for the auto-communication shit
        /// </summary>
        [Group("communications", "Auto-communication messages")]
        public sealed class CommunicationModule
        {

        }


        private sealed class ChatAutoCompleteHandler : AutocompleteHandler
        {
            public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
                IInteractionContext context,
                IAutocompleteInteraction autocompleteInteraction,
                IParameterInfo parameter,
                IServiceProvider services)
            {
                var results = new List<AutocompleteResult>()
                {
                    new AutocompleteResult("Markov", "markov"),
                };
                return await Task.FromResult(AutocompletionResult.FromSuccess(results));
            }
        }

        private sealed class MacroAutocompleteHandler : AutocompleteHandler
        {

            public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
                IInteractionContext context,
                IAutocompleteInteraction autocompleteInteraction,
                IParameterInfo parameter,
                IServiceProvider services)
            {
                var macroService = (MacroService)services.GetService(typeof(MacroService));

                var macros = await macroService.GetServerMacros(context.Guild);
                var results = new List<AutocompleteResult>();
                foreach (var macro in macros)
                {
                    results.Add(new AutocompleteResult(macro.Macro, macro.Macro));
                }
                return await Task.FromResult(AutocompletionResult.FromSuccess(results));
            }
        }

    }
}
