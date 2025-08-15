using ClickerGame.Upgrades.Domain.Enums;
using ClickerGame.Upgrades.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace ClickerGame.Upgrades.Domain.Entities
{
    public class Upgrade
    {
        [Key]
        public string UpgradeId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public UpgradeCategory Category { get; set; }
        public UpgradeRarity Rarity { get; set; }

        [MaxLength(200)]
        public string? IconUrl { get; set; }

        public UpgradeCost Cost { get; set; } = new(BigNumber.One);
        public List<UpgradeEffect> Effects { get; set; } = new();
        public List<UpgradePrerequisite> Prerequisites { get; set; } = new();

        public int MaxLevel { get; set; } = 1000;
        public bool IsActive { get; set; } = true;
        public bool IsHidden { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public ICollection<PlayerUpgrade> PlayerUpgrades { get; set; } = new List<PlayerUpgrade>();

        public bool CanPlayerPurchase(
            Guid playerId,
            BigNumber playerScore,
            int playerLevel,
            BigNumber clickCount,
            Dictionary<string, int> ownedUpgrades)
        {
            if (!IsActive || IsHidden) return false;

            var currentLevel = ownedUpgrades.GetValueOrDefault(UpgradeId, 0);
            if (currentLevel >= MaxLevel) return false;

            // Check prerequisites
            if (Prerequisites.Any(prereq => !prereq.IsSatisfied(playerLevel, playerScore, clickCount, ownedUpgrades)))
                return false;

            // Check cost
            var requiredCost = Cost.CalculateCostAtLevel(currentLevel);
            return playerScore >= requiredCost;
        }

        public BigNumber GetTotalEffectForCategory(UpgradeCategory category, int level)
        {
            return Effects
                .Where(effect => effect.TargetCategory == category)
                .Aggregate(BigNumber.Zero, (total, effect) => total + effect.CalculateEffectAtLevel(level));
        }
    }
}