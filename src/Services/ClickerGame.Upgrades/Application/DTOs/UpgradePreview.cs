using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class UpgradePreview
    {
        public string UpgradeId { get; init; } = string.Empty;
        public int CurrentLevel { get; init; }
        public int NewLevel { get; init; }
        public BigNumber TotalCost { get; init; }
        public BigNumber CurrentEffect { get; init; }
        public BigNumber NewEffect { get; init; }
        public BigNumber EffectIncrease { get; init; }
        public PlayerEffectSummary NewPlayerEffects { get; init; } = new();
        public bool CanAfford { get; init; }
        public List<string> PrerequisiteWarnings { get; init; } = new();
    }
}