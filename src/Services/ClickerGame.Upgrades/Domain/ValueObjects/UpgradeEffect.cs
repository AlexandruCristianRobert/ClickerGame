using ClickerGame.Upgrades.Domain.Enums;

namespace ClickerGame.Upgrades.Domain.ValueObjects
{
    public record UpgradeEffect
    {
        public UpgradeCategory TargetCategory { get; init; }
        public UpgradeType EffectType { get; init; }
        public BigNumber BaseValue { get; init; }
        public decimal ScalingFactor { get; init; }
        public decimal MaxEffect { get; init; }
        public string Description { get; init; } = string.Empty;

        public UpgradeEffect(
            UpgradeCategory targetCategory,
            UpgradeType effectType,
            BigNumber baseValue,
            decimal scalingFactor = 1.0m,
            decimal maxEffect = decimal.MaxValue,
            string description = "")
        {
            TargetCategory = targetCategory;
            EffectType = effectType;
            BaseValue = baseValue;
            ScalingFactor = scalingFactor;
            MaxEffect = maxEffect;
            Description = description;
        }

        public BigNumber CalculateEffectAtLevel(int level)
        {
            if (level <= 0) return BigNumber.Zero;

            return EffectType switch
            {
                UpgradeType.Linear => BaseValue * level,
                UpgradeType.Exponential => BaseValue * (decimal)Math.Pow((double)ScalingFactor, level),
                UpgradeType.Percentage => BaseValue * level,
                UpgradeType.Compound => BaseValue * (decimal)Math.Pow((double)(1 + ScalingFactor), level),
                UpgradeType.Threshold => level >= ScalingFactor ? BaseValue : BigNumber.Zero,
                UpgradeType.OneTime => level > 0 ? BaseValue : BigNumber.Zero,
                _ => BigNumber.Zero
            };
        }
    }
}