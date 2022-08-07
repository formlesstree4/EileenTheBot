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

        private const string AGREE = "👍";
        private const string DISAGREE = "👎";
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
        private readonly Timer _maintenanceTimer;


        /// <summary>
        ///     Creates a new instance of the <see cref="BetterPaginationService"/> which is used to render paginated, embedded messages.
        /// </summary>
        /// <param name="dsc">A reference to the <see cref="DiscordSocketClient"/></param>
        /// <param name="logger">A logging function</param>
        public BetterPaginationService(DiscordSocketClient dsc, ILogger<BetterPaginationService> logger)
        {
            _messages = new ConcurrentDictionary<ulong, BetterPaginationMessage>();
            _maintenanceTimer = new Timer(HandleMaintenance, null, 2000, 2000);
            logger.LogInformation("Initializing...");
            _client = dsc;
            this.logger = logger;
            _client.ReactionAdded += OnReactionAdded;
            _client.MessageDeleted += OnMessageDeleted;
            logger.LogInformation("{reaction} and {message} have been hooked", nameof(_client.ReactionAdded), nameof(_client.MessageDeleted));
        }



        /// <summary>
        ///     Disposes of the <see cref="BetterPaginationService"/>, cleaning up any references.
        /// </summary>
        public void Dispose()
        {
            _client.ReactionAdded -= OnReactionAdded;
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
                await context.Interaction.RespondAsync(embed: message.CurrentPage);
                var paginatedMessage = await context.Interaction.GetOriginalResponseAsync();
#pragma warning disable CS4014
                Task.Run(async () =>
                {
                    await paginatedMessage.AddReactionsAsync(new[] { new Emoji(FIRST), new Emoji(BACK), new Emoji(NEXT), new Emoji(END), new Emoji(STOP) });
                    logger.LogTrace("Monitoring {messageId}", paginatedMessage.Id);
                });
#pragma warning restore CS4014
                _messages.TryAdd(paginatedMessage.Id, message);
                return paginatedMessage;
            }
            catch (Discord.Net.HttpException httpEx)
            {
                logger.LogError(httpEx, "An error occurred sending the paginated message");
                return null;
            }
        }

        /// <summary>
        ///     Handles incoming reaction additions.
        /// </summary>
        /// <param name="messageParam">A possibly cached instance of a <see cref="IUserMessage"/></param>
        /// <param name="channel">The <see cref="ISocketMessageChannel"/> implementation that the reaction was added from</param>
        /// <param name="reaction">A reference to the <see cref="SocketReaction"/></param>
        /// <returns>A promise to react to the <see cref="SocketReaction"/></returns>
        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageParam, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            var message = await (messageParam.GetOrDownloadAsync());

            if (!await HandleMessageValidation(message, reaction)) return;
            if (!_messages.TryGetValue(message.Id, out BetterPaginationMessage betterMessage))
            {
                logger.LogWarning("An expired message was reacted to. Discarding / ignoring for the time being");
                await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                return;
            }

            try
            {
                logger.LogTrace("Removing {reaction} from the message.", reaction.Emote.Name);
                await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            }
            catch (Discord.Net.HttpException httpEx)
            {
                logger.LogError(httpEx, "An error occurred sending the paginated message");
                return;
            }

            // Now that we're here, we can do everything we need to do.
            logger.LogTrace("Invoking {service} to handle the details.", nameof(HandleEmojiReaction));
            await HandleEmojiReaction(message, reaction, betterMessage);

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

        /// <summary>
        ///     Performs some validation of the incoming <see cref="IUserMessage"/> that was reacted to.
        /// </summary>
        /// <param name="message">The <see cref="IUserMessage"/> implementation that was reacted to</param>
        /// <param name="reaction">The <see cref="SocketReaction"/> which contains the reaction details</param>
        /// <returns>A boolean value that indicates if <paramref name="message"/> can be processed</returns>
        private async Task<bool> HandleMessageValidation(IUserMessage message, SocketReaction reaction)
        {
            if (message is null)
            {
                logger.LogTrace("The message was not found, or, something went wrong retrieveing the message. This is informational but if it keeps happening it might be noteworthy");
                return false;
            }
            if (!reaction.User.IsSpecified)
            {
                logger.LogTrace("The message {messageId} did not have a User specified", message.Id);
                return false;
            }
            if (!_messages.TryGetValue(message.Id, out BetterPaginationMessage betterMessage))
            {
                logger.LogTrace("The message {messageId} was not a known tracked message", message.Id);
                return false;
            }
            if (!betterMessage.User.Id.Equals(reaction.UserId))
            {
                logger.LogWarning("Expected User ID: {exected}. Actual User ID: {actual}. Discarding", betterMessage.User.Id, reaction.UserId);
                //await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Expected User ID: {betterMessage.User.Id}. Actual User ID: {reaction.UserId}. Discarding."));
                if (!reaction.UserId.Equals(_client.CurrentUser.Id))
                {
                    await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                }
                return false;
            }
            return true;
        }

        /// <summary>
        ///     Ensures <paramref name="message"/> contains the necessary <see cref="Emoji"/>
        /// </summary>
        /// <param name="message">The <see cref="IUserMessage"/> implementation to verify</param>
        /// <returns></returns>
        private static async Task EnsureMessageHasReactions(IUserMessage message)
        {
            if (!message.Reactions.ContainsKey(new Emoji(FIRST))) await message.AddReactionAsync(new Emoji(FIRST));
            if (!message.Reactions.ContainsKey(new Emoji(BACK))) await message.AddReactionAsync(new Emoji(BACK));
            if (!message.Reactions.ContainsKey(new Emoji(NEXT))) await message.AddReactionAsync(new Emoji(NEXT));
            if (!message.Reactions.ContainsKey(new Emoji(END))) await message.AddReactionAsync(new Emoji(END));
            if (!message.Reactions.ContainsKey(new Emoji(STOP))) await message.AddReactionAsync(new Emoji(STOP));
        }

        /// <summary>
        ///     Handles the various reaction branches.
        /// </summary>
        /// <param name="message">The <see cref="IUserMessage"/> implementation that was reacted to</param>
        /// <param name="reaction">The <see cref="SocketReaction"/> which contains the reaction details</param>
        /// <param name="betterMessage">The <see cref="BetterPaginationMessage"/> that's the backing data source for <see cref="IUserMessage"/></param>
        /// <returns></returns>
        private async Task HandleEmojiReaction(IUserMessage message, SocketReaction reaction, BetterPaginationMessage betterMessage)
        {
            var purge = false; var show = false; var updateMessage = false;
            switch (reaction.Emote.Name)
            {
                case FIRST:
                    if (betterMessage.CurrentPageIndex == 0) return;
                    //await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Jumping to Page 0."));
                    logger.LogTrace("Message {id}: Jumping to Page 0", message.Id);
                    betterMessage.CurrentPageIndex = 0; updateMessage = true;
                    break;
                case BACK:
                    if (betterMessage.CurrentPageIndex == 0) return;
                    logger.LogTrace("Message {id}: Moving to Page {page}", message.Id, betterMessage.CurrentPageIndex - 1);
                    betterMessage.CurrentPageIndex--; updateMessage = true;
                    break;
                case NEXT:
                    if (betterMessage.CurrentPageIndex == betterMessage.Pages.Count - 1) return;
                    logger.LogTrace("Message {id}: Moving to Page {page}", message.Id, betterMessage.CurrentPageIndex + 1);
                    betterMessage.CurrentPageIndex++; updateMessage = true;
                    break;
                case END:
                    if (betterMessage.CurrentPageIndex == betterMessage.Pages.Count - 1) return;
                    logger.LogTrace("Message {id}: Moving to Page {page}", message.Id, betterMessage.Pages.Count - 1);
                    betterMessage.CurrentPageIndex = betterMessage.Pages.Count - 1; updateMessage = true;
                    break;
                case STOP:
                case DISAGREE:
                    purge = true;
                    logger.LogTrace("Message {id}: Deletion Request", message.Id);
                    break;
                case AGREE:
                    logger.LogTrace("Message {id} is now visible to {channel}", message.Id, message.Channel.Name);
                    show = true; updateMessage = true;
                    break;
                default:
                    logger.LogTrace("Invalid or unknown reaction ({reaction})", reaction.Emote.Name);
                    break;
            }
            if (purge)
            {
                logger.LogTrace("Attempting to delete {messageId}", message.Id);
                await message.DeleteAsync();
                return;
            }
            if (updateMessage)
            {
                logger.LogInformation("Message {messageId} is moving to Page {currentPageIndex}", message.Id, betterMessage.CurrentPageIndex);
                await message.ModifyAsync(msg => msg.Embed = betterMessage.CurrentPage);

                if (show && !message.Reactions.ContainsKey(new Emoji(FIRST)))
                {
                    await message.RemoveAllReactionsAsync();
                    await EnsureMessageHasReactions(message);
                }

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
