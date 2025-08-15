using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class PurchaseUpgradeRequest
    {
        public string UpgradeId { get; init; } = string.Empty;
        public int LevelsToPurchase { get; init; } = 1;
        public BigNumber MaxSpendAmount { get; init; } = BigNumber.Zero;
        public bool ConfirmPurchase { get; init; } = true;
        public bool AutoPurchasePrerequisites { get; init; } = false;
    }

    public class BulkPurchaseRequest
    {
        public List<PurchaseUpgradeRequest> Purchases { get; init; } = new();
        public BigNumber MaxTotalSpend { get; init; } = BigNumber.Zero;
        public bool OptimizeOrder { get; init; } = true;
    }

    public class PreviewUpgradeRequest
    {
        public string UpgradeId { get; init; } = string.Empty;
        public int LevelsToPurchase { get; init; } = 1;
        public bool IncludeEffectBreakdown { get; init; } = true;
    }
}