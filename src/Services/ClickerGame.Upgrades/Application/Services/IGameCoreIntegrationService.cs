using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.Services
{
    public interface IGameCoreIntegrationService
    {
        Task<BigNumber> GetPlayerScoreAsync(Guid playerId);
        Task<bool> DeductPlayerScoreAsync(Guid playerId, BigNumber amount, string reason = "Upgrade Purchase");
        Task<bool> ApplyUpgradeEffectsAsync(Guid playerId, GameCoreUpgradeEffects effects);
        Task<bool> ValidatePlayerSessionAsync(Guid playerId);
        Task<GameSessionInfo?> GetPlayerGameSessionAsync(Guid playerId);
    }

    public class GameCoreUpgradeEffects
    {
        public decimal ClickPowerBonus { get; init; }
        public decimal PassiveIncomeBonus { get; init; }
        public decimal MultiplierBonus { get; init; } = 1.0m;
        public string SourceUpgradeId { get; init; } = string.Empty;
    }

    public class GameSessionInfo
    {
        public Guid SessionId { get; init; }
        public Guid PlayerId { get; init; }
        public string PlayerUsername { get; init; } = string.Empty;
        public BigNumber Score { get; init; }
        public long ClickCount { get; init; }
        public BigNumber ClickPower { get; init; }
        public decimal PassiveIncomePerSecond { get; init; }
        public bool IsActive { get; init; }
    }
}