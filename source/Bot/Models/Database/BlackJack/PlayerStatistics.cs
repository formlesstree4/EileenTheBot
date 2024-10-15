namespace Bot.Models.Database.BlackJack;

public class PlayerStatisticsModel
{
   public ulong user_id { get; set; }
   public ulong total_rounds_played { get; set; }
   public ulong total_rounds_won { get; set; }
   public ulong total_rounds_lost { get; set; }
   public decimal total_money_bet { get; set; }
   public decimal total_money_won { get; set; }
   public decimal total_money_lost { get; set; }
}
