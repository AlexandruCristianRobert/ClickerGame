using ClickerGame.GameCore.Domain.Entities;
using ClickerGame.GameCore.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ClickerGame.GameCore.Infrastructure.Data
{
    public class GameCoreDbContext : DbContext
    {
        public GameCoreDbContext(DbContextOptions<GameCoreDbContext> options) : base(options)
        {
        }

        public DbSet<GameSession> GameSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<GameSession>(entity =>
            {
                entity.HasKey(e => e.SessionId);
                entity.HasIndex(e => e.PlayerId).IsUnique();

                // Convert BigNumber to string for storage
                entity.Property(e => e.Score)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<BigNumber>(v, (JsonSerializerOptions?)null));

                entity.Property(e => e.ClickPower)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<BigNumber>(v, (JsonSerializerOptions?)null));

                entity.Property(e => e.PlayerUsername).HasMaxLength(50);
                entity.Property(e => e.GameStateJson).HasColumnType("nvarchar(max)");
            });
        }
    }
}