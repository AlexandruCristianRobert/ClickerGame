using ClickerGame.Upgrades.Domain.Entities;
using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ClickerGame.Upgrades.Infrastructure.Data
{
    public class UpgradesDbContext : DbContext
    {
        public UpgradesDbContext(DbContextOptions<UpgradesDbContext> options) : base(options)
        {
        }

        public DbSet<Upgrade> Upgrades { get; set; }
        public DbSet<PlayerUpgrade> PlayerUpgrades { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Upgrade entity configuration
            modelBuilder.Entity<Upgrade>(entity =>
            {
                entity.HasKey(e => e.UpgradeId);
                entity.Property(e => e.UpgradeId).HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.IconUrl).HasMaxLength(200);

                // Configure enum properties
                entity.Property(e => e.Category)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                entity.Property(e => e.Rarity)
                    .HasConversion<string>()
                    .HasMaxLength(20);

                // Configure value object properties as JSON
                entity.Property(e => e.Cost)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<UpgradeCost>(v, (JsonSerializerOptions?)null)!)
                    .HasColumnType("nvarchar(max)");

                entity.Property(e => e.Effects)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<UpgradeEffect>>(v, (JsonSerializerOptions?)null)!)
                    .HasColumnType("nvarchar(max)");

                entity.Property(e => e.Prerequisites)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<UpgradePrerequisite>>(v, (JsonSerializerOptions?)null)!)
                    .HasColumnType("nvarchar(max)");

                // Configure indexes
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Rarity);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => new { e.Category, e.IsActive });
            });

            // PlayerUpgrade entity configuration
            modelBuilder.Entity<PlayerUpgrade>(entity =>
            {
                entity.HasKey(e => e.PlayerUpgradeId);

                entity.Property(e => e.UpgradeId)
                    .IsRequired()
                    .HasMaxLength(50);

                // Configure relationships
                entity.HasOne(e => e.Upgrade)
                    .WithMany(u => u.PlayerUpgrades)
                    .HasForeignKey(e => e.UpgradeId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Configure indexes
                entity.HasIndex(e => e.PlayerId);
                entity.HasIndex(e => new { e.PlayerId, e.UpgradeId }).IsUnique();
                entity.HasIndex(e => e.PurchasedAt);
            });

            // NOTE: Seed data moved to separate service to avoid EF Core limitations with complex value objects
        }
    }
}