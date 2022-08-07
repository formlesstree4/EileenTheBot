using Bot.Services.RavenDB;
using Bot.TypeReaders;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Bot.Services
{
    public sealed class CommandHandlingService : IEileenService
    {
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;
        private readonly ILogger<CommandHandlingService> logger;
        private readonly ServerConfigurationService _serverConfiguration;
        private readonly MacroService macroService;
        private readonly char _defaultPrefix;

        public CommandHandlingService(
            CommandService commandService,
            DiscordSocketClient client,
            ServerConfigurationService serverConfiguration,
            RavenDatabaseService ravenDatabaseService,
            MacroService macroService,
            IServiceProvider provider,
            ILogger<CommandHandlingService> logger)
        {
            _commands = commandService;
            _discord = client;
            _serverConfiguration = serverConfiguration;
            _defaultPrefix = ravenDatabaseService.Configuration.CommandPrefix;
            this.macroService = macroService;
            _services = provider;
            this.logger = logger;

            // Hook CommandExecuted to handle post-command-execution logic.
            _commands.CommandExecuted += CommandExecutedAsync;
            _commands.Log += (log) =>
            {
                switch (log.Severity)
                {
#pragma warning disable CA2254
                    case LogSeverity.Critical:
                        logger.LogCritical(log.Exception, log.Message);
                        break;
                    case LogSeverity.Debug:
                        logger.LogDebug(log.Exception, log.Message);
                        break;
                    case LogSeverity.Error:
                        logger.LogError(log.Exception, log.Message);
                        break;
                    case LogSeverity.Info:
                        logger.LogInformation(log.Exception, log.Message);
                        break;
                    case LogSeverity.Verbose:
                        logger.LogTrace(log.Exception, log.Message);
                        break;
                    case LogSeverity.Warning:
                        logger.LogWarning(log.Exception, log.Message);
                        break;
#pragma warning restore CA2254
                }
                return Task.CompletedTask;
            };

            // Hook MessageReceived so we can process each message to see
            // if it qualifies as a command.
            _discord.MessageReceived += MessageReceivedAsync;
        }

        public async Task InitializeService()
        {
            // Register modules that are public and inherit ModuleBase<T>.
            _commands.AddTypeReader(typeof(string[]), new StringArrayTypeReader());
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        public bool AutoInitialize() => false;

        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            // Ignore system messages, or messages from other bots
            if (rawMessage is not SocketUserMessage message) return;
            if (message.Source != MessageSource.User) return;

            // This value holds the offset where the prefix ends
            var argPos = 0;
            var prefix = _defaultPrefix;

            if (message.Channel is SocketGuildChannel sgc)
            {
                prefix = await GetGuildPrefix(sgc.Guild);
                logger.LogTrace("Overriding Command Prefix for {guildId} (from {default} to {new})", sgc.Guild.Id, _defaultPrefix, prefix);
            }

            // Perform prefix check. You may want to replace this with
            // (!message.HasMentionPrefix(_discord.CurrentUser, ref argPos))
            // for a less traditional command format like @bot help.
            if (!message.HasCharPrefix(prefix, ref argPos)) return;

            var context = new SocketCommandContext(_discord, message);
            // Perform the execution of the command. In this method,
            // the command service will perform precondition and parsing check
            // then execute the command if one is matched.
            await _commands.ExecuteAsync(context, argPos, _services);
            // Note that normally a result will be returned by this format, but here
            // we will handle the result in CommandExecutedAsync,
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            // command is unspecified when there was a search failure (command not found); we don't care about these errors
            if (!command.IsSpecified)
            {
                // could look to see if it's a new macro
                if (context.Guild != null)
                {
                    var userMessage = context.Message.Resolve();
                    var prefix = await GetGuildPrefix(context.Guild);
                    if (userMessage.StartsWith(prefix))
                    {
                        userMessage = userMessage.Remove(0, 1);
                    }
                    var serverMacro = await macroService.TryGetMacroAsync(context.Guild, userMessage);
                    if (serverMacro.Item1)
                    {
                        await context.Message.ReplyAsync(serverMacro.Item2.Response);
                    }
                }
                return;
            }

            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess) return;

            // the command failed, let's notify the user that something happened.
            await context.Channel.SendMessageAsync($"error: {result.ErrorReason}");
        }

        private async Task<char> GetGuildPrefix(IGuild guild)
        {
            var serverConfig = await _serverConfiguration.GetOrCreateConfigurationAsync(guild);
            return serverConfig.CommandPrefix;
        }

    }
}
