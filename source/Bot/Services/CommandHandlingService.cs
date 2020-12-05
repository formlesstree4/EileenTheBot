using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Bot.Services.RavenDB;
using Bot.TypeReaders;

namespace Bot.Services
{
    public sealed class CommandHandlingService
    {
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;
        private readonly ServerConfigurationService _serverConfiguration;
        private readonly Func<LogMessage, Task> _logger;
        private readonly char _defaultPrefix;

        public CommandHandlingService(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _serverConfiguration = services.GetRequiredService<ServerConfigurationService>();
            _defaultPrefix = services.GetRequiredService<RavenDatabaseService>().Configuration.CommandPrefix;
            _logger = services.GetRequiredService<Func<LogMessage, Task>>();
            _services = services;

            // Hook CommandExecuted to handle post-command-execution logic.
            _commands.CommandExecuted += CommandExecutedAsync;
            _commands.Log += _logger;
            // Hook MessageReceived so we can process each message to see
            // if it qualifies as a command.
            _discord.MessageReceived += MessageReceivedAsync;
        }

        public async Task InitializeAsync()
        {
            // Register modules that are public and inherit ModuleBase<T>.
            _commands.AddTypeReader(typeof(string[]), new StringArrayTypeReader());
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            // Ignore system messages, or messages from other bots
            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;         

            // This value holds the offset where the prefix ends
            var argPos = 0;
            var prefix = _defaultPrefix;

            if (message.Channel is SocketGuildChannel sgc)
            {
                var serverConfig = await _serverConfiguration.GetOrCreateConfigurationAsync(sgc.Guild);
                prefix = serverConfig.CommandPrefix;
                Write($"Overriding Command Prefix for {sgc.Guild.Id} (from {_defaultPrefix} to {prefix})");
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
            if (!command.IsSpecified) return;

            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess) return;

            // the command failed, let's notify the user that something happened.
            await context.Channel.SendMessageAsync($"error: {result.ErrorReason}");
        }

        private void Write(string message, LogSeverity severity = LogSeverity.Info)
        {
            _logger(new LogMessage(severity, nameof(CommandHandlingService), message));
        }

    }
}