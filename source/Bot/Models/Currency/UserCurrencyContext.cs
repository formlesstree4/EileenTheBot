using Microsoft.EntityFrameworkCore;

namespace Bot.Models.Currency
{
    public class UserCurrencyContext : DbContext
    {
        public DbSet<UserCurrency> Users { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=Users.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserCurrency>(eb =>
            {
                eb.HasKey(c => new { c.PlayerId, c.ServerId });
            }
            );
        }


    }
}