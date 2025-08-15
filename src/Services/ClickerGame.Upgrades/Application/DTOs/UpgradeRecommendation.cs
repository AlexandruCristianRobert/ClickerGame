using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class UpgradeRecommendation
    {
        public string UpgradeId { get; init; } = string.Empty;
        public string UpgradeName { get; init; } = string.Empty;
        public int RecommendedLevels { get; init; }
        public BigNumber TotalCost { get; init; }
        public decimal EfficiencyScore { get; init; }
        public BigNumber ExpectedEffectIncrease { get; init; }
        public string Reasoning { get; init; } = string.Empty;
    }
}