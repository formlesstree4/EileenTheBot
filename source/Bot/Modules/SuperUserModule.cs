using Bot.Preconditions;
using Bot.Services;
using Discord.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Modules
{

    [Group("admin"), TrustedUsersPrecondition]
    public class SuperUserModule : ModuleBase<SocketCommandContext>
    {
        private readonly ServerConfigurationService serverConfigurationService;
        private readonly CancellationTokenSource cancellationTokenSource;

        public SuperUserModule(
            ServerConfigurationService serverConfigurationService,
            CancellationTokenSource cancellationTokenSource)
        {
            this.serverConfigurationService = serverConfigurationService ?? throw new System.ArgumentNullException(nameof(serverConfigurationService));
            this.cancellationTokenSource = cancellationTokenSource ?? throw new System.ArgumentNullException(nameof(cancellationTokenSource));
        }

        [Command("chat"),
        Summary("Sets the responder type for the server"),
        RequireContext(ContextType.Guild)]
        public async Task SetResponderAsync(
            [Name("Type"),
            Summary("The type of chat responder to use. Supported ones are: gpt, markov")]string type = "")
        {
            var scs = await serverConfigurationService.GetOrCreateConfigurationAsync(Context.Guild);
            switch (type.ToLowerInvariant())
            {
                case "gpt":
                    scs.ResponderType = Models.ServerConfigurationData.AutomatedResponseType.GPT;
                    await serverConfigurationService.SaveServiceAsync();
                    await ReplyAsync($"This Guild is now using {scs.ResponderType}");
                    break;
                case "markov":
                    scs.ResponderType = Models.ServerConfigurationData.AutomatedResponseType.Markov;
                    await serverConfigurationService.SaveServiceAsync();
                    await ReplyAsync($"This Guild is now using {scs.ResponderType}");
                    break;
                default:
                    await ReplyAsync($"This Guild is currently using {scs.ResponderType}");
                    break;
            }
        }

        [Command("shutdown")]
        public async Task KillAsync()
        {
            await Context.Channel.SendMessageAsync("Initiating shutdown request...");
            cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Sub-module for handling Service interactions
        /// </summary>
        [Group("services")]
        public sealed class ServiceModule : ModuleBase<SocketCommandContext>
        {
            private readonly ServiceManager serviceManager;

            public ServiceModule(ServiceManager serviceManager)
            {
                this.serviceManager = serviceManager;
            }


            [Command("save"), Summary("Forcibly saves any given collection of services")]
            public async Task SaveService([Summary("The collection of services, separated by spaces")] params string[] names)
            {
                var services = new List<IEileenService>(GetQualifyingServices(names));
                await ReplyAsync("Saving services, please wait...");
                foreach (var service in services)
                {
                    await service.SaveServiceAsync();
                }
                await ReplyAsync("All services have been saved!");
            }

            [Command("load"), Summary("Forcibly loads any given collection of services")]
            public async Task LoadService([Summary("The collection of services, separated by spaces")] params string[] names)
            {
                var services = new List<IEileenService>(GetQualifyingServices(names));
                await ReplyAsync("Reloading services, please wait...");
                foreach (var service in services)
                {
                    await service.LoadServiceAsync();
                }
                await ReplyAsync("All services have been reloaded!");
            }

            [Command("list"), Summary("Lists all the currently running services in Eileen")]
            public async Task ListAsync()
            {
                var services = serviceManager.GetServiceNames();
                await ReplyAsync($"The following services are currently running: {string.Join(", ", services)}");
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


    }
}