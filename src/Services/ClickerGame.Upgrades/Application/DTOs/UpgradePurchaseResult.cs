using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class UpgradePurchaseResult
    {
        public bool Success { get; init; }
        public string UpgradeId { get; init; } = string.Empty;
        public int LevelsPurchased { get; init; }
        public int NewLevel { get; init; }
        public BigNumber TotalCostPaid { get; init; }
        public BigNumber RemainingScore { get; init; }
        public PlayerEffectSummary UpdatedEffects { get; init; } = new();
        public List<string> Messages { get; init; } = new();
        public List<string> Errors { get; init; } = new();
        public DateTime PurchasedAt { get; init; } = DateTime.UtcNow;
    }

    public class BulkUpgradePurchaseResult
    {
        public bool OverallSuccess { get; init; }
        public List<UpgradePurchaseResult> PurchaseResults { get; init; } = new();
        public BigNumber TotalSpent { get; init; }
        public BigNumber RemainingScore { get; init; }
        public PlayerEffectSummary FinalEffects { get; init; } = new();
        public int SuccessfulPurchases { get; init; }
        public int FailedPurchases { get; init; }
        public List<string> OverallMessages { get; init; } = new();
    }
}