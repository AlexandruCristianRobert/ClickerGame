using ClickerGame.Players.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClickerGame.Players.Infrastructure.Data
{
    public class PlayersDbContext : DbContext
    {
        public PlayersDbContext(DbContextOptions<PlayersDbContext> options) : base(options)
        {
            
        }

        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerProfile> PlayerProfiles { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(p => p.PlayerId);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();

                entity.HasOne(e => e.Profile)
                .WithOne(p => p.Player)
                .HasForeignKey<PlayerProfile>(p => p.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.RefreshTokens)
                .WithOne(r => r.Player)
                .HasForeignKey(r => r.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PlayerProfile>(entity =>
            {
                entity.HasKey(e => e.ProfileId);
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.TokenId);
                entity.HasIndex(e => e.Token).IsUnique();
            });
        }
    }
}
