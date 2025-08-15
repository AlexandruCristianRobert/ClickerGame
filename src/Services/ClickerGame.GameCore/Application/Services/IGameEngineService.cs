using ClickerGame.GameCore.Domain.Entities;
using ClickerGame.GameCore.Domain.ValueObjects;

namespace ClickerGame.GameCore.Application.Services
{
    public interface IGameEngineService
    {
        Task<BigNumber> ProcessClickAsync(Guid playerId, BigNumber clickPower);
        Task<GameSession> GetGameSessionAsync(Guid playerId);
        Task<GameSession> CreateGameSessionAsync(Guid playerId, string username);
        Task<BigNumber> CalculateOfflineEarningsAsync(Guid playerId);
        Task SaveGameSessionAsync(GameSession session);
        Task<bool> ValidatePlayerAsync(Guid playerId, string token);
    }
}