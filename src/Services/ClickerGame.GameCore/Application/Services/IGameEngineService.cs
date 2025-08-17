using ClickerGame.GameCore.Domain.Entities;
using ClickerGame.GameCore.Domain.ValueObjects;
using ClickerGame.GameCore.Application.DTOs;

namespace ClickerGame.GameCore.Application.Services
{
    public interface IGameEngineService
    {
        // Core game mechanics
        Task<BigNumber> ProcessClickAsync(Guid playerId, BigNumber clickPower);
        Task<GameSession> GetGameSessionAsync(Guid playerId);
        Task<GameSession> CreateGameSessionAsync(Guid playerId, string username);
        Task<BigNumber> CalculateOfflineEarningsAsync(Guid playerId);
        Task SaveGameSessionAsync(GameSession session);
        Task<bool> ValidatePlayerAsync(Guid playerId, string token);

        // Enhanced real-time features
        Task<BigNumber> ProcessPassiveIncomeAsync(Guid playerId);
        Task<bool> ApplyUpgradeEffectsAsync(Guid playerId, UpgradeEffectsDto upgradeEffects);
    }
}