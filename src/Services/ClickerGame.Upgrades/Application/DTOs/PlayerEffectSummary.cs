using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Domain.Enums;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class PlayerEffectSummary
    {
        public BigNumber TotalClickPowerBonus { get; init; } = BigNumber.Zero;
        public BigNumber TotalPassiveIncomeBonus { get; init; } = BigNumber.Zero;
        public decimal TotalMultiplier { get; init; } = 1.0m;
        public Dictionary<UpgradeCategory, BigNumber> CategoryEffects { get; init; } = new();
        public Dictionary<string, BigNumber> UpgradeContributions { get; init; } = new();
        public int TotalUpgradeLevel { get; init; }
        public DateTime CalculatedAt { get; init; } = DateTime.UtcNow;
    }
}