using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Services
{


    /// <summary>
    ///     Defines a service that is responsible for handling paginated messages to <see cref="IUser"/>
    /// </summary>
    [Summary("A simplified pagination service that handles rotating embeds using Reactions")]
    public sealed class BetterPaginationService : IDisposable, IEileenService
    {
        private const string FIRST = "⏮";
        private const string BACK = "◀";
        private const string NEXT = "▶";
        private const string END = "⏭";
        private const string STOP = "⏹";


        private readonly Color ErrorColor = new(237, 67, 55);
        private readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);


        private readonly ConcurrentDictionary<ulong, BetterPaginationMessage> _messages;
        private readonly DiscordSocketClient _client;
        private readonly ILogger<BetterPaginationService> logger;
        private readonly InteractionHandlingService interactionHandlingService;
        private readonly Timer _maintenanceTimer;


        /// <summary>
        ///     Creates a new instance of the <see cref="BetterPaginationService"/> which is used to render paginated, embedded messages.
        /// </summary>
        /// <param name="dsc">A reference to the <see cref="DiscordSocketClient"/></param>
        /// <param name="logger">A logging function</param>
        public BetterPaginationService(
            DiscordSocketClient dsc,
            ILogger<BetterPaginationService> logger,
            InteractionHandlingService interactionHandlingService)
        {
            _messages = new ConcurrentDictionary<ulong, BetterPaginationMessage>();
            _maintenanceTimer = new Timer(HandleMaintenance, null, 2000, 2000);
            logger.LogInformation("Initializing...");
            _client = dsc;
            this.logger = logger;
            this.interactionHandlingService = interactionHandlingService;
            _client.MessageDeleted += OnMessageDeleted;
            logger.LogInformation("{reaction} and {message} have been hooked", nameof(_client.ReactionAdded), nameof(_client.MessageDeleted));
        }



        /// <summary>
        ///     Disposes of the <see cref="BetterPaginationService"/>, cleaning up any references.
        /// </summary>
        public void Dispose()
        {
            _client.MessageDeleted -= OnMessageDeleted;
        }

        /// <summary>
        ///     Sends a <see cref="BetterPaginationMessage"/> to the specified <see cref="IMessageChannel"/>
        /// </summary>
        /// <param name="channel">An implementation of <see cref="IMessageChannel"/></param>
        /// <param name="message">An instance of <see cref="BetterPaginationMessage"/> to send</param>
        /// <returns>A promise of the <see cref="IUserMessage"/></returns>
        public async Task<IUserMessage> Send(IInteractionContext context, IMessageChannel channel, BetterPaginationMessage message)
        {
            logger.LogInformation("Sending paginated message to {channel}", channel.Name);
            try
            {
                logger.LogTrace("{message}", message.ToJson());
                return await CreateUserMessageAndButtons(context, message);
            }
            catch (Discord.Net.HttpException httpEx)
            {
                logger.LogError(httpEx, "An error occurred sending the paginated message");
                return null;
            }
        }

        /// <summary>
        ///     Handles removing messages.
        /// </summary>
        /// <param name="messageParam">A possibly cached instance of a <see cref="IUserMessage"/></param>
        /// <param name="channel">The <see cref="ISocketMessageChannel"/> implementation that the message was deleted from</param>
        /// <returns>A promise to react to the deletion</returns>
        private async Task OnMessageDeleted(Cacheable<IMessage, ulong> messageParam, Cacheable<IMessageChannel, ulong> channel)
        {
            try
            {
                var message = await messageParam.GetOrDownloadAsync();
                if (message is null)
                {
                    logger.LogTrace("{messageId} was not found in cache and could not be downloaded", messageParam.Id);
                    _messages.TryRemove(messageParam.Id, out var _);
                    return;
                }
                var removed = _messages.TryRemove(messageParam.Id, out BetterPaginationMessage betterMessage);
                if (!removed)
                {
                    logger.LogTrace("{messageId} was not a tracked message. Disregard", message.Id);
                    return;
                }
                logger.LogTrace("{messageId} was removed from the internal tracking system.", message.Id);
                return;
            }
            catch (NullReferenceException nre)
            {
                logger.LogError(nre, "A null reference exception was generated and caught while processing {eventName}", nameof(OnMessageDeleted));
            }
            catch (Exception e)
            {
                logger.LogError(e, "A generic error was caught while processing {eventName}", nameof(OnMessageDeleted));
            }
        }

        private void HandleMaintenance(object state)
        {
            var messageIds = _messages.Keys.ToList();
            var messages = _messages.Values.ToList();
            for (var index = _messages.Count - 1; index >= 0; index--)
            {
                var messageId = messageIds[index];
                var message = messages[index];
                if ((DateTime.UtcNow - message.Created).Duration() > Timeout)
                {
                    _messages.Remove(messageId, out _);
                }
            }
        }

        private async Task<IUserMessage> CreateUserMessageAndButtons(IInteractionContext context, BetterPaginationMessage message)
        {
            var messageGuid = Guid.NewGuid();
            var buttonBuilder = CreateButtonBuilder(message, messageGuid);
            await context.Interaction.RespondAsync(embed: message.CurrentPage, components: buttonBuilder.Build());
            var discordMessage = await context.Interaction.GetOriginalResponseAsync();
            HookUpInteractionButtons(messageGuid, message, discordMessage);
            return discordMessage;
        }

        private void HookUpInteractionButtons(
            Guid messageGuid,
            BetterPaginationMessage message,
            IUserMessage discordMessage)
        {
            _messages.TryAdd(discordMessage.Id, message);

            interactionHandlingService.RegisterCallbackHandler($"FIRST-{messageGuid}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (!_messages.TryGetValue(smc.Message.Id, out var m)) return;
                if (m.User != null && m.User.Id != smc.User.Id) return;
                m.CurrentPageIndex = 0;
                await smc.UpdateAsync(p => { p.Embed = m.CurrentPage; p.Components = CreateButtonBuilder(message, messageGuid).Build(); });
            }));
            interactionHandlingService.RegisterCallbackHandler($"BACK-{messageGuid}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (!_messages.TryGetValue(smc.Message.Id, out var m)) return;
                if (m.User != null && m.User.Id != smc.User.Id) return;
                m.CurrentPageIndex--;
                await smc.UpdateAsync(p => { p.Embed = m.CurrentPage; p.Components = CreateButtonBuilder(message, messageGuid).Build(); });
            }));
            interactionHandlingService.RegisterCallbackHandler($"NEXT-{messageGuid}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (!_messages.TryGetValue(smc.Message.Id, out var m)) return;
                if (m.User != null && m.User.Id != smc.User.Id) return;
                m.CurrentPageIndex++;
                await smc.UpdateAsync(p => { p.Embed = m.CurrentPage; p.Components = CreateButtonBuilder(message, messageGuid).Build(); });
            }));
            interactionHandlingService.RegisterCallbackHandler($"END-{messageGuid}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (!_messages.TryGetValue(smc.Message.Id, out var m)) return;
                if (m.User != null && m.User.Id != smc.User.Id) return;
                m.CurrentPageIndex = m.Pages.Count - 1;
                await smc.UpdateAsync(p => { p.Embed = m.CurrentPage; p.Components = CreateButtonBuilder(message, messageGuid).Build(); });
            }));
            interactionHandlingService.RegisterCallbackHandler($"STOP-{messageGuid}", new InteractionButtonCallbackProvider(async smc =>
            {
                if (!_messages.TryGetValue(smc.Message.Id, out var m)) return;
                if (m.User != null && m.User.Id != smc.User.Id) return;
                _messages.TryRemove(smc.Message.Id, out _);
                await smc.Message.DeleteAsync();
                interactionHandlingService.RemoveButtonCallbacks(
                    $"FIRST-{messageGuid}",
                    $"BACK-{messageGuid}",
                    $"NEXT-{messageGuid}",
                    $"END-{messageGuid}",
                    $"STOP-{messageGuid}");
            }));
        }

        private static ComponentBuilder CreateButtonBuilder(BetterPaginationMessage message, Guid messageGuid)
        {
            return new ComponentBuilder()
                .WithButton(emote: new Emoji(FIRST), customId: $"FIRST-{messageGuid}")
                .WithButton(emote: new Emoji(BACK), customId: $"BACK-{messageGuid}", disabled: message.CurrentPageIndex == 0)
                .WithButton(emote: new Emoji(NEXT), customId: $"NEXT-{messageGuid}", disabled: message.CurrentPageIndex == message.Pages.Count - 1)
                .WithButton(emote: new Emoji(END), customId: $"END-{messageGuid}")
                .WithButton(emote: new Emoji(STOP), customId: $"STOP-{messageGuid}");
        }


    }

    /// <summary>
    ///     Contains all paginated content.
    /// </summary>
    public sealed class BetterPaginationMessage
    {
        private readonly List<Embed> _pages;
        private int _currentPage;



        /// <summary>
        ///     Gets a collection of <see cref="Embed"/> messages that are to be used when paginating.
        /// </summary>
        public IReadOnlyCollection<Embed> Pages => _pages.AsReadOnly();

        /// <summary>
        ///     Gets or sets the current page.
        /// </summary>
        public int CurrentPageIndex
        {
            get { return _currentPage; }
            set
            {
                if (value < 0) value = 0;
                if (value > Pages.Count - 1) value = Pages.Count - 1;
                _currentPage = value;
            }
        }

        /// <summary>
        ///     Gets the current <see cref="Embed"/> that should be rendered.
        /// </summary>
        public Embed CurrentPage => Pages.ElementAtOrDefault(CurrentPageIndex);

        /// <summary>
        ///     Gets or sets whether or not this <see cref="BetterPaginationMessage"/> contains NSFW content.
        /// </summary>
        public bool IsNsfw { get; set; } = false;

        /// <summary>
        ///     Gets or sets the <see cref="IUser"/> that created this message.
        /// </summary>
        public IUser User { get; }

        /// <summary>
        ///     Gets the UTC timestamp this instance was created.
        /// </summary>
        /// <value></value>
        public DateTime Created { get; }



        /// <summary>
        ///     Creates a new instance of the <see cref="BetterPaginationMessage"/> class for rendering a set of paginated messages.
        /// </summary>
        /// <param name="pages">The collection of <see cref="Embed"/> messages</param>
        public BetterPaginationMessage(IEnumerable<Embed> pages, bool pageCountAsInline = true, IUser user = null, string pageText = "Page")
        {
            Created = DateTime.UtcNow;
            var embedList = new List<Embed>(pages);
            User = user;

            // If rewrite is set to true, we basically have to
            // completely rebuild the embedded messages from nothing.
            if (pageCountAsInline)
            {
                for (var pg = 0; pg < embedList.Count; pg++)
                {
                    var embed = embedList[pg];
                    var eBuilder = new EmbedBuilder();
                    foreach (var field in embed.Fields)
                    {
                        eBuilder.AddField(field.Name, field.Value, field.Inline);
                    }
                    eBuilder.AddField(pageText, $"{pg + 1}/{embedList.Count:N0}", true);

                    if (embed.Author != null)
                    {
                        eBuilder.Author = new EmbedAuthorBuilder()
                            .WithIconUrl(embed.Author.Value.IconUrl)
                            .WithName(embed.Author.Value.Name)
                            .WithUrl(embed.Author.Value.Url);
                    }
                    eBuilder.Color = embed.Color;
                    eBuilder.Description = embed.Description;
                    if (embed.Footer != null)
                    {
                        eBuilder.Footer = new EmbedFooterBuilder()
                            .WithText(embed.Footer.Value.Text);
                    }
                    if (embed.Image != null)
                    {
                        eBuilder.ImageUrl = embed.Image.Value.Url;
                    }
                    if (embed.Thumbnail != null)
                    {
                        eBuilder.ThumbnailUrl = embed.Thumbnail.Value.Url;
                    }
                    eBuilder.WithCurrentTimestamp();
                    eBuilder.Title = embed.Title;
                    eBuilder.Url = embed.Url;
                    embedList[pg] = eBuilder.Build();

                }
            }

            _pages = embedList;

        }

        public override string ToString()
        {
            var sBuilder = new StringBuilder();
            sBuilder.AppendLine($"Pages: {_pages.Count} ");
            sBuilder.AppendLine($"Current Page: {CurrentPageIndex} ");
            sBuilder.AppendLine($"IsNsfw: {IsNsfw}");
            return sBuilder.ToString();
        }

    }


}
