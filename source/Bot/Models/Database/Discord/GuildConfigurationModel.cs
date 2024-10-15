namespace Bot.Models.Database.Discord;

public class GuildConfigurationModel
{
    public ulong guild_id { get; set; }
    public ulong[]? trusted_users { get; set; }
    public bool enabled { get; set; }
}
