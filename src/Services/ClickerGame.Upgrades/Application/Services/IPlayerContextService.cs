using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.Services
{
    public interface IPlayerContextService
    {
        Task<PlayerUpgradeContext> GetPlayerContextAsync(Guid playerId);
        Task<BigNumber> GetPlayerScoreAsync(Guid playerId);
        Task<bool> DeductPlayerScoreAsync(Guid playerId, BigNumber amount);
        Task<Dictionary<string, int>> GetPlayerUpgradeLevelsAsync(Guid playerId);
        Task<bool> ValidatePlayerExistsAsync(Guid playerId);
        Task UpdatePlayerLastActiveAsync(Guid playerId);
        Task<bool> ApplyUpgradeEffectsToGameCoreAsync(Guid playerId, PlayerEffectSummary effects, string sourceUpgradeId);
    }
}