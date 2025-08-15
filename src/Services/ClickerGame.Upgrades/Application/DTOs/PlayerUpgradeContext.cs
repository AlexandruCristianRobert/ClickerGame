using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.DTOs
{
    public class PlayerUpgradeContext
    {
        public Guid PlayerId { get; init; }
        public BigNumber CurrentScore { get; init; }
        public int PlayerLevel { get; init; }
        public BigNumber ClickCount { get; init; }
        public Dictionary<string, int> OwnedUpgrades { get; init; } = new();
        public PlayerEffectSummary CurrentEffects { get; init; } = new();
        public DateTime LastActiveAt { get; init; } = DateTime.UtcNow;
        public int TotalUpgradeLevel => OwnedUpgrades.Values.Sum();
    }
}