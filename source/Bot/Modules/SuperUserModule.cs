using Bot.Models.ChannelCommunication;
using Bot.Preconditions;
using Bot.Services;
using Bot.Services.Communication.Responders;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
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
            switch (type.ToLower())
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
            public async Task TrainChain([Summary("length", "How many historical messages to pull")]int length = 100)
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
        public sealed class CommunicationModule : InteractionModuleBase
        {
            private readonly ChannelCommunicationService channelCommunicationService;
            private readonly InteractionHandlingService interactionHandlingService;

            public CommunicationModule(
                ChannelCommunicationService channelCommunicationService, InteractionHandlingService interactionHandlingService)
            {
                this.channelCommunicationService = channelCommunicationService ?? throw new ArgumentNullException(nameof(channelCommunicationService));
                this.interactionHandlingService = interactionHandlingService ?? throw new ArgumentNullException(nameof(interactionHandlingService));
            }


            [SlashCommand("create", "Creates a repeatable message to run on the given CRON job", runMode: RunMode.Async), RequireContext(ContextType.Guild)]
            public async Task ScheduleRepeatableMessage()
            {
                var callbackId = $"repeat-message-{Guid.NewGuid()}";
                var modalBuilder = new ModalBuilder()
                    .WithTitle($"Create Repeatable Message (for channel #{Context.Channel.Name})")
                    .WithCustomId(callbackId)
                    .AddTextInput("Message Name", "job-name", placeholder: "A unique name for the job. If left blank, a random ID will be generated instead")
                    .AddTextInput("CRON expression", "cron-string", placeholder: "A valid CRON expression", required: true)
                    .AddTextInput("Message", "repeat-message", placeholder: "This is the message you will have sent", required: true);

                interactionHandlingService.RegisterCallbackHandler(callbackId, new InteractionModalCallbackProvider(async (context) =>
                {
                    var jobName = context.Data.Components.First(d => d.CustomId.Equals("job-name", StringComparison.OrdinalIgnoreCase)).Value;
                    var cron = context.Data.Components.First(d => d.CustomId.Equals("cron-string", StringComparison.OrdinalIgnoreCase)).Value;
                    var message = context.Data.Components.First(d => d.CustomId.Equals("repeat-message", StringComparison.OrdinalIgnoreCase)).Value;

                    if (string.IsNullOrWhiteSpace(jobName))
                    {
                        jobName = Guid.NewGuid().ToString();
                    }

                    bool isCronExpression = false;
                    Cronos.CronExpression cronExpression = null;

                    try
                    {
                        cronExpression = Cronos.CronExpression.Parse(cron);
                        isCronExpression = true;
                    }
                    catch (Exception) { }

                    await channelCommunicationService.ScheduleNewTask(
                        Context.Guild,
                        new ChannelCommuncationJobEntry
                        {
                            ChannelId = Context.Channel.Id,
                            Created = DateTime.Now,
                            GuildId = Context.Guild.Id,
                            HasRun = false,
                            JobName = jobName,
                            Message = message,
                            Repeats = isCronExpression,
                            WhenToRun = cron
                        });

                    if (isCronExpression)
                    {
                        await context.RespondAsync($"Your job has been scheduled successfully and will run at {cronExpression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"))}. Job ID: {jobName}", ephemeral: true);
                    }
                    else
                    {
                        await context.RespondAsync($"Your job has been scheduled successfully and will run in approximately {cron} minute(s). Job ID: {jobName}", ephemeral: true);
                    }
                }, true));

                await RespondWithModalAsync(modalBuilder.Build());
            }

            [SlashCommand("remove", "Removes a scheduled message", runMode: RunMode.Async), RequireContext(ContextType.Guild)]
            public async Task RemoveRepeatableMessage(
                [Summary("Job", "The name of the job to cancel"), Autocomplete(typeof(MessageRemoverAutocompleteHandler))] string jobName)
            {
                // Falkenhoof: Instead of remove job why not put wolf job instead
                await channelCommunicationService.RemoveJob(Context.Guild, jobName);
                await RespondAsync($"Job {jobName} has been removed", ephemeral: true);
            }

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
                var macroService = services.GetRequiredService<MacroService>();
                var macros = await macroService.GetServerMacros(context.Guild);
                var results = new List<AutocompleteResult>();
                foreach (var macro in macros)
                {
                    results.Add(new AutocompleteResult(macro.Macro, macro.Macro));
                }
                return await Task.FromResult(AutocompletionResult.FromSuccess(results));
            }
        }

        private sealed class MessageRemoverAutocompleteHandler : AutocompleteHandler
        {
            public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
                IInteractionContext context,
                IAutocompleteInteraction autocompleteInteraction,
                IParameterInfo parameter,
                IServiceProvider services)
            {
                var ccs = services.GetRequiredService<ChannelCommunicationService>();
                var results = await ccs.GetServerJobs(context.Guild);
                return AutocompletionResult.FromSuccess(results.Select(r => new AutocompleteResult(r.JobName, r.JobName)));
            }
        }

    }
}
