using ClickerGame.Shared.Events;

namespace ClickerGame.Upgrades.Domain.Events
{
    public class UpgradeUnlockedEvent : IntegrationEvent
    {
        public Guid PlayerId { get; }
        public string UpgradeId { get; }
        public string UpgradeName { get; }
        public DateTime UnlockedAt { get; }

        public UpgradeUnlockedEvent(
            Guid playerId,
            string upgradeId,
            string upgradeName)
        {
            PlayerId = playerId;
            UpgradeId = upgradeId;
            UpgradeName = upgradeName;
            UnlockedAt = DateTime.UtcNow;
        }
    }
}