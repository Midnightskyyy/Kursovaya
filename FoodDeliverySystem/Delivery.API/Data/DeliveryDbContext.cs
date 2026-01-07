using Microsoft.EntityFrameworkCore;
using Delivery.API.Entities;

namespace Delivery.API.Data
{
    public class DeliveryDbContext : DbContext
    {
        public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options) { }

        public DbSet<Courier> Couriers { get; set; }
        public DbSet<DeliveryEntity> Deliveries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Courier>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
                entity.Property(e => e.VehicleType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Rating).HasColumnType("decimal(3,2)");

                entity.HasMany(e => e.Deliveries)
                    .WithOne(e => e.Courier)
                    .HasForeignKey(e => e.CourierId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<DeliveryEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrderId).IsUnique();
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DeliveryAddress).IsRequired().HasMaxLength(500);
                entity.Property(e => e.PickupAddress).HasMaxLength(500);
                entity.Property(e => e.Notes).HasMaxLength(1000);
            });
        }
    }
}