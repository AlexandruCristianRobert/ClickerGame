using ClickerGame.Upgrades.Domain.Enums;

namespace ClickerGame.Upgrades.Domain.ValueObjects
{
    public record UpgradePrerequisite
    {
        public PrerequisiteType Type { get; init; }
        public string? RequiredUpgradeId { get; init; }
        public BigNumber RequiredValue { get; init; }
        public int RequiredLevel { get; init; }
        public string Description { get; init; } = string.Empty;

        public UpgradePrerequisite(
            PrerequisiteType type,
            BigNumber requiredValue,
            int requiredLevel = 1,
            string? requiredUpgradeId = null,
            string description = "")
        {
            Type = type;
            RequiredValue = requiredValue;
            RequiredLevel = requiredLevel;
            RequiredUpgradeId = requiredUpgradeId;
            Description = description;
        }

        public bool IsSatisfied(
            int playerLevel,
            BigNumber totalScore,
            BigNumber clickCount,
            Dictionary<string, int> ownedUpgrades)
        {
            return Type switch
            {
                PrerequisiteType.PlayerLevel => playerLevel >= RequiredLevel,
                PrerequisiteType.TotalScore => totalScore >= RequiredValue,
                PrerequisiteType.ClickCount => clickCount >= RequiredValue,
                PrerequisiteType.OtherUpgrade => !string.IsNullOrEmpty(RequiredUpgradeId) &&
                    ownedUpgrades.ContainsKey(RequiredUpgradeId) &&
                    ownedUpgrades[RequiredUpgradeId] >= RequiredLevel,
                PrerequisiteType.Achievement => true, // TODO: Integrate with achievement service
                PrerequisiteType.Timeplayed => true, // TODO: Integrate with player time tracking
                _ => false
            };
        }
    }
}