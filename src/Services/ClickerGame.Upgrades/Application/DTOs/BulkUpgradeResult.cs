using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class BulkUpgradeResult
    {
        public string UpgradeId { get; init; } = string.Empty;
        public int LevelsCanAfford { get; init; }
        public BigNumber TotalCost { get; init; }
        public BigNumber RemainingBudget { get; init; }
        public BigNumber EffectIncrease { get; init; }
        public bool IsOptimal { get; init; }
    }
}