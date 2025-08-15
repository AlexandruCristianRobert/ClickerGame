using ClickerGame.Upgrades.Domain.Enums;

namespace ClickerGame.Upgrades.Domain.ValueObjects
{
    public record UpgradeCost
    {
        public BigNumber BaseCost { get; init; }
        public decimal CostMultiplier { get; init; }
        public UpgradeType CostType { get; init; }
        public BigNumber MaxCost { get; init; }

        public UpgradeCost(
            BigNumber baseCost,
            decimal costMultiplier = 1.15m,
            UpgradeType costType = UpgradeType.Exponential,
            BigNumber maxCost = default)
        {
            BaseCost = baseCost;
            CostMultiplier = costMultiplier;
            CostType = costType;
            MaxCost = maxCost == default ? new BigNumber(decimal.MaxValue) : maxCost;
        }

        public BigNumber CalculateCostAtLevel(int currentLevel)
        {
            if (currentLevel < 0) return BaseCost;

            var cost = CostType switch
            {
                UpgradeType.Linear => BaseCost * (1 + currentLevel * CostMultiplier),
                UpgradeType.Exponential => BaseCost * (decimal)Math.Pow((double)CostMultiplier, currentLevel),
                UpgradeType.Compound => BaseCost * (decimal)Math.Pow((double)(1 + CostMultiplier), currentLevel),
                UpgradeType.OneTime => currentLevel > 0 ? new BigNumber(decimal.MaxValue) : BaseCost,
                _ => BaseCost
            };

            return cost > MaxCost ? MaxCost : cost;
        }

        public BigNumber CalculateTotalCostForLevels(int fromLevel, int toLevel)
        {
            var totalCost = BigNumber.Zero;
            for (int level = fromLevel; level < toLevel; level++)
            {
                totalCost = totalCost + CalculateCostAtLevel(level);
            }
            return totalCost;
        }
    }
}
