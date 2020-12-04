using Discord;
using Discord.WebSocket;
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
    public sealed class BetterPaginationService : IDisposable
    {

        private const string AGREE = "👍";
        private const string DISAGREE = "👎";
        private const string FIRST = "⏮";
        private const string BACK = "◀";
        private const string NEXT = "▶";
        private const string END = "⏭";
        private const string STOP = "⏹";


        private readonly Color ErrorColor = new Color(237, 67, 55);
        private readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);


        private readonly ConcurrentDictionary<ulong, BetterPaginationMessage> _messages;
        private readonly DiscordSocketClient _client;
        private readonly Func<LogMessage, Task> WriteLog;
        private readonly Timer _maintenanceTimer;


        /// <summary>
        ///     Creates a new instance of the <see cref="BetterPaginationService"/> which is used to render paginated, embedded messages.
        /// </summary>
        /// <param name="dsc">A reference to the <see cref="DiscordSocketClient"/></param>
        /// <param name="logger">A logging function</param>
        public BetterPaginationService(DiscordSocketClient dsc, Func<LogMessage, Task> logger = null)
        {
            _messages = new ConcurrentDictionary<ulong, BetterPaginationMessage>();
            _maintenanceTimer = new Timer(HandleMaintenance, null, 2000, 2000);
            WriteLog = logger ?? (message => Task.CompletedTask);
            WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), "Initializing..."));
            _client = dsc;
            _client.ReactionAdded += OnReactionAdded;
            _client.MessageDeleted += OnMessageDeleted;
            WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"{nameof(DiscordSocketClient.ReactionAdded)} has been hooked."));
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
        public async Task<IUserMessage> Send(IMessageChannel channel, BetterPaginationMessage message)
        {
            await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Sending paginated message to {channel.Name}"));
            try
            {
                await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"{message}"));
                var paginatedMessage = await channel.SendMessageAsync(embed: message.CurrentPage);
                await paginatedMessage.AddReactionsAsync(new[] {new Emoji(FIRST), new Emoji(BACK), new Emoji(NEXT), new Emoji(END), new Emoji(STOP)});
                await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Monitoring {paginatedMessage.Id}"));
                _messages.TryAdd(paginatedMessage.Id, message);
                return paginatedMessage;
            }
            catch (Discord.Net.HttpException httpEx)
            {
                await WriteLog(new LogMessage(LogSeverity.Critical, nameof(BetterPaginationService),
                    $"An error occurred sending the paginated message. The message has thus been discarded. Error: {httpEx.Message}. Reason Provided: {httpEx.Reason}", httpEx));
                await WriteLog(new LogMessage(LogSeverity.Critical, nameof(BetterPaginationService), $"{Newtonsoft.Json.JsonConvert.SerializeObject(message.CurrentPage)}"));
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
        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> messageParam, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var message = await (messageParam.GetOrDownloadAsync());

            if (!await HandleMessageValidation(message, reaction)) return;
            if (!_messages.TryGetValue(message.Id, out BetterPaginationMessage betterMessage)) 
            {
                await WriteLog(new LogMessage(LogSeverity.Warning, nameof(BetterPaginationService), "An expired message was reacted to. Discarding / ignoring for the time being"));
                await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                return;
            }

            try
            {
                await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"Removing {reaction.Emote.Name} from the message."));
                await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            }
            catch (Discord.Net.HttpException httpEx)
            {
                await WriteLog(new LogMessage(LogSeverity.Critical, nameof(BetterPaginationService), $"An error occurred sending the paginated message: {httpEx.Message}"));
                return;
            }

            // Now that we're here, we can do everything we need to do.
            await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"Invoking {nameof(HandleEmojiReaction)} to handle the details."));
            await HandleEmojiReaction(message, reaction, betterMessage);

        }

        /// <summary>
        ///     Handles removing messages.
        /// </summary>
        /// <param name="messageParam">A possibly cached instance of a <see cref="IUserMessage"/></param>
        /// <param name="channel">The <see cref="ISocketMessageChannel"/> implementation that the message was deleted from</param>
        /// <returns>A promise to react to the deletion</returns>
        private async Task OnMessageDeleted(Cacheable<IMessage, ulong> messageParam, ISocketMessageChannel channel)
        {
            try
            {
                var message = await messageParam.GetOrDownloadAsync();
                if (ReferenceEquals(message, null))
                {
                    await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"{message.Id} was not found in cache and could not be downloaded. Disregard."));
                    return;
                }
                var removed = _messages.TryRemove(messageParam.Id, out BetterPaginationMessage betterMessage);
                if (!removed)
                {
                    await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"{message.Id} was not a tracked message. Disregard."));
                    return;
                }
                await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"{message.Id} was removed from the internal tracking system."));
                return;
            }
            catch (System.NullReferenceException nre)
            {
                await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"Null Reference Exception occurred inside the {nameof(OnMessageDeleted)} handler", nre));
            }
            catch (System.Exception e)
            {
                await WriteLog(new LogMessage(LogSeverity.Warning, nameof(BetterPaginationService), $"Captured a generic error while executing {nameof(OnMessageDeleted)}", e));
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
            if (ReferenceEquals(message, null))
            {
                await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"{nameof(message)} was not found in cache and could not be downloaded. Disregard."));
                return false;
            }
            if (!reaction.User.IsSpecified)
            {
                await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"{nameof(message)} ID {message.Id} had no User specified. Discarding."));
                return false;
            }
            if (!_messages.TryGetValue(message.Id, out BetterPaginationMessage betterMessage))
            {
                await WriteLog(new LogMessage(LogSeverity.Verbose, nameof(BetterPaginationService), $"{nameof(message)} ID {message.Id} was not found in the {nameof(ConcurrentDictionary<ulong, BetterPaginationMessage>)}. Discarding."));
                return false;
            }
            if (!betterMessage.User.Id.Equals(reaction.UserId))
            {
                await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Expected User ID: {betterMessage.User.Id}. Actual User ID: {reaction.UserId}. Discarding."));
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
        private async Task EnsureMessageHasReactions(IUserMessage message)
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
                    await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Jumping to Page 0."));
                    betterMessage.CurrentPageIndex = 0; updateMessage = true;
                    break;
                case BACK:
                    if (betterMessage.CurrentPageIndex == 0) return;
                    await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Moving to Page {betterMessage.CurrentPageIndex - 1}."));
                    betterMessage.CurrentPageIndex--; updateMessage = true;
                    break;
                case NEXT:
                    if (betterMessage.CurrentPageIndex == betterMessage.Pages.Count - 1) return;
                    await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Moving to Page {betterMessage.CurrentPageIndex + 1}."));
                    betterMessage.CurrentPageIndex++; updateMessage = true;
                    break;
                case END:
                    if (betterMessage.CurrentPageIndex == betterMessage.Pages.Count - 1) return;
                    await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Jumping to Page {betterMessage.Pages.Count - 1}."));
                    betterMessage.CurrentPageIndex = betterMessage.Pages.Count - 1; updateMessage = true;
                    break;
                case STOP:
                case DISAGREE:
                    purge = true;
                    await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Message {message.Id} is to be deleted."));
                    break;
                case AGREE:
                    await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"{message.Channel.Name} has now agreed to display {message.Id}."));
                    show = true; updateMessage = true;
                    break;
                default:
                    await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Invalid Reaction ({reaction.Emote.Name} specified."));
                    break;
            }
            if (purge)
            {
                await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Requesting Discord to delete {message.Id}."));
                await message.DeleteAsync();
                return;
            }
            if (updateMessage)
            {
                await WriteLog(new LogMessage(LogSeverity.Info, nameof(BetterPaginationService), $"Updating {message.Id} to Page {betterMessage.CurrentPageIndex}."));
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
            for(var index = _messages.Count - 1; index >= 0; index--)
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

                    if(embed.Author != null)
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
                    if(embed.Image != null)
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