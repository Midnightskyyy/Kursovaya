using Microsoft.EntityFrameworkCore;
using Payment.API.Entities;

namespace Payment.API.Data
{
    public class PaymentDbContext : DbContext
    {
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

        public DbSet<UserCard> UserCards { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserCard>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.Property(e => e.CardLastFourDigits).IsRequired().HasMaxLength(4);
                entity.Property(e => e.CardHolderName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.TokenHash).IsRequired();

                entity.HasMany(e => e.Transactions)
                    .WithOne(e => e.Card)
                    .HasForeignKey(e => e.CardId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrderId).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Amount).HasColumnType("decimal(10,2)");
                entity.Property(e => e.ProviderTransactionId).HasMaxLength(255);
                entity.Property(e => e.FailureReason).HasMaxLength(500);
            });
        }
    }
}