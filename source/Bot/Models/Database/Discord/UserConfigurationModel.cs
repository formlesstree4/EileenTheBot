using System;

namespace Bot.Models.Database.Discord;

public sealed class UserConfigurationModel
{
    public ulong user_id { get; set; }
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
    public ulong money { get; set; }
    public ulong loaned { get; set; }
    public bool admin { get; set; }
}
