using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Domain.Enums;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class PlayerUpgradeProgressDto
    {
        public Guid PlayerId { get; init; }
        public int TotalUpgradesOwned { get; init; }
        public int TotalUpgradeLevels { get; init; }
        public BigNumber TotalSpent { get; init; }
        public Dictionary<UpgradeCategory, int> UpgradesByCategory { get; init; } = new();
        public Dictionary<UpgradeRarity, int> UpgradesByRarity { get; init; } = new();
        public PlayerEffectSummary CurrentEffects { get; init; } = new();
        public int UnlockedUpgrades { get; init; }
        public int AvailableUpgrades { get; init; }
        public List<PlayerUpgradeDto> RecentPurchases { get; init; } = new();
        public DateTime LastPurchaseAt { get; init; }
        public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
    }

    public class PlayerUpgradeDto
    {
        public Guid PlayerUpgradeId { get; init; }
        public string UpgradeId { get; init; } = string.Empty;
        public string UpgradeName { get; init; } = string.Empty;
        public int Level { get; init; }
        public BigNumber CurrentEffect { get; init; }
        public DateTime PurchasedAt { get; init; }
        public DateTime LastUpgradedAt { get; init; }
        public bool CanUpgrade { get; init; }
        public BigNumber NextLevelCost { get; init; }
    }
}