using System;

namespace Bot.Models.Database.BlackJack;

public class TableModel
{
    public Guid table_id { get; set; }
    public ulong guild_id { get; set; }
    public ulong channel_id { get; set; }
    public ulong thread_id { get; set; }
    public bool active { get; set; }
    public ulong rounds_played { get; set; }
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
}
