using ClickerGame.Upgrades.Domain.Enums;
using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class UpgradeDto
    {
        public string UpgradeId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public UpgradeCategory Category { get; init; }
        public UpgradeRarity Rarity { get; init; }
        public string? IconUrl { get; init; }
        public int MaxLevel { get; init; }
        public bool IsActive { get; init; }
        public bool IsHidden { get; init; }
        public DateTime CreatedAt { get; init; }

        // Current player context
        public int CurrentLevel { get; init; }
        public bool CanPurchase { get; init; }
        public BigNumber NextLevelCost { get; init; }
        public BigNumber NextLevelEffect { get; init; }
        public List<UpgradeEffectDto> Effects { get; init; } = new();
        public List<UpgradePrerequisiteDto> Prerequisites { get; init; } = new();
        public bool PrerequisitesMet { get; init; }
        public List<string> PrerequisiteWarnings { get; init; } = new();
    }
}