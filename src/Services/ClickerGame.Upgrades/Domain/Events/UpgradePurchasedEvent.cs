using ClickerGame.Shared.Events;
using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Domain.Events
{
    public class UpgradePurchasedEvent : IntegrationEvent
    {
        public Guid PlayerId { get; }
        public string UpgradeId { get; }
        public int NewLevel { get; }
        public int LevelsPurchased { get; }
        public BigNumber CostPaid { get; }
        public DateTime PurchasedAt { get; }

        public UpgradePurchasedEvent(
            Guid playerId,
            string upgradeId,
            int newLevel,
            int levelsPurchased,
            BigNumber costPaid)
        {
            PlayerId = playerId;
            UpgradeId = upgradeId;
            NewLevel = newLevel;
            LevelsPurchased = levelsPurchased;
            CostPaid = costPaid;
            PurchasedAt = DateTime.UtcNow;
        }
    }
}