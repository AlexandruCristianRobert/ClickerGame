using ClickerGame.Upgrades.Domain.Enums;
using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class UpgradeEfficiencyAnalysis
    {
        public Guid PlayerId { get; init; }
        public List<UpgradeEfficiencyItem> EfficiencyRankings { get; init; } = new();
        public UpgradeRecommendation TopRecommendation { get; init; } = new();
        public BigNumber OptimalBudgetAllocation { get; init; }
        public decimal OverallEfficiencyScore { get; init; }
        public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
    }

    public class UpgradeEfficiencyItem
    {
        public string UpgradeId { get; init; } = string.Empty;
        public string UpgradeName { get; init; } = string.Empty;
        public decimal EfficiencyScore { get; init; }
        public BigNumber CostPerEffect { get; init; }
        public int RecommendedPriority { get; init; }
    }

    public class UpgradeStatistics
    {
        public string UpgradeId { get; init; } = string.Empty;
        public int TotalOwners { get; init; }
        public double AverageLevel { get; init; }
        public int MaxLevel { get; init; }
        public int TotalLevels { get; init; }
        public DateTime LastPurchase { get; init; }
        public BigNumber TotalSpent { get; init; }
    }

    public class UpgradeEffectDto
    {
        public UpgradeCategory TargetCategory { get; init; }
        public UpgradeType EffectType { get; init; }
        public BigNumber BaseValue { get; init; }
        public decimal ScalingFactor { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    public class UpgradePrerequisiteDto
    {
        public PrerequisiteType Type { get; init; }
        public string? RequiredUpgradeId { get; init; }
        public BigNumber RequiredValue { get; init; }
        public int RequiredLevel { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}