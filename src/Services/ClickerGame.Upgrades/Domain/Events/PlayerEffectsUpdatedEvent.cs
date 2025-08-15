using ClickerGame.Shared.Events;
using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Domain.Events
{
    public class PlayerEffectsUpdatedEvent : IntegrationEvent
    {
        public Guid PlayerId { get; }
        public Dictionary<string, BigNumber> EffectValues { get; }
        public BigNumber TotalClickPowerBonus { get; }
        public BigNumber TotalPassiveIncomeBonus { get; }
        public decimal TotalMultiplier { get; }

        public PlayerEffectsUpdatedEvent(
            Guid playerId,
            Dictionary<string, BigNumber> effectValues,
            BigNumber totalClickPowerBonus,
            BigNumber totalPassiveIncomeBonus,
            decimal totalMultiplier)
        {
            PlayerId = playerId;
            EffectValues = effectValues;
            TotalClickPowerBonus = totalClickPowerBonus;
            TotalPassiveIncomeBonus = totalPassiveIncomeBonus;
            TotalMultiplier = totalMultiplier;
        }
    }
}