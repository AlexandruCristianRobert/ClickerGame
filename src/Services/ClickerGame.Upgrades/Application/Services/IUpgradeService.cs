using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.Entities;
using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Domain.Enums;

namespace ClickerGame.Upgrades.Application.Services
{
    public interface IUpgradeService
    {
        // Upgrade Management
        Task<UpgradeDto> GetUpgradeAsync(string upgradeId);
        Task<IEnumerable<UpgradeDto>> GetAvailableUpgradesAsync(Guid playerId);
        Task<IEnumerable<UpgradeDto>> GetUpgradesByCategoryAsync(UpgradeCategory category, Guid playerId);

        // Player Upgrade Operations
        Task<UpgradePurchaseResult> PurchaseUpgradeAsync(Guid playerId, PurchaseUpgradeRequest request);
        Task<BulkUpgradePurchaseResult> PurchaseUpgradeBulkAsync(Guid playerId, BulkPurchaseRequest request);
        Task<UpgradePreview> PreviewUpgradePurchaseAsync(Guid playerId, PreviewUpgradeRequest request);

        // Player Progress
        Task<PlayerUpgradeProgressDto> GetPlayerUpgradeProgressAsync(Guid playerId);
        Task<PlayerEffectSummary> CalculatePlayerEffectsAsync(Guid playerId);
        Task<IEnumerable<PlayerUpgradeDto>> GetPlayerUpgradesAsync(Guid playerId);

        // Recommendations & Analytics
        Task<UpgradeRecommendation> GetUpgradeRecommendationAsync(Guid playerId, BigNumber budget);
        Task<IEnumerable<UpgradeRecommendation>> GetTopUpgradeRecommendationsAsync(Guid playerId, int count = 5);
        Task<UpgradeEfficiencyAnalysis> AnalyzeUpgradeEfficiencyAsync(Guid playerId);

        // Administrative
        Task<bool> UnlockUpgradeForPlayerAsync(Guid playerId, string upgradeId);
        Task ResetPlayerUpgradesAsync(Guid playerId);
        Task<UpgradeStatistics> GetUpgradeStatisticsAsync(string upgradeId);
    }
}