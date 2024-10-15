using System;

namespace Bot.Models.Database.BlackJack;

public class GuildConfigurationModel
{
    public ulong guild_id { get; set; }
    public ulong[]? managers { get; set; }
    public ulong[]? allowed_channels { get; set; }
    public DateTime? created { get; set; }
    public DateTime? updated { get; set; }
    public decimal max_loaned_amount { get; set; }
}
