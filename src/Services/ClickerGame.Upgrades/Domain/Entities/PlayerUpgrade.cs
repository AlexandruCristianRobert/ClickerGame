using System.ComponentModel.DataAnnotations;

namespace ClickerGame.Upgrades.Domain.Entities
{
    public class PlayerUpgrade
    {
        [Key]
        public Guid PlayerUpgradeId { get; set; }

        public Guid PlayerId { get; set; }

        [Required]
        public string UpgradeId { get; set; } = string.Empty;

        public int Level { get; set; }
        public DateTime PurchasedAt { get; set; }
        public DateTime LastUpgradedAt { get; set; }

        // Navigation properties
        public Upgrade Upgrade { get; set; } = null!;

        public PlayerUpgrade()
        {
            PlayerUpgradeId = Guid.NewGuid();
            PurchasedAt = DateTime.UtcNow;
            LastUpgradedAt = DateTime.UtcNow;
        }

        public void UpgradeLevel(int levels = 1)
        {
            if (Level + levels > Upgrade.MaxLevel)
                throw new InvalidOperationException($"Cannot upgrade beyond max level {Upgrade.MaxLevel}");

            Level += levels;
            LastUpgradedAt = DateTime.UtcNow;
        }

        public bool CanUpgrade(int levels = 1)
        {
            return Level + levels <= Upgrade.MaxLevel;
        }
    }
}