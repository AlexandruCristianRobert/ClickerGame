using ClickerGame.Upgrades.Application.DTOs;

namespace ClickerGame.Upgrades.Application.Services
{
    public interface IAntifraudService
    {
        Task<FraudAssessment> AssessPurchaseRiskAsync(Guid playerId, PurchaseUpgradeRequest request);
        Task RecordPurchaseAttemptAsync(Guid playerId, string upgradeId, bool successful, string? reason = null);
        Task<bool> IsPlayerFlaggedAsync(Guid playerId);
        Task FlagPlayerForReviewAsync(Guid playerId, string reason);
        Task<PurchaseRateInfo> GetPurchaseRateInfoAsync(Guid playerId);
    }

    public class FraudAssessment
    {
        public decimal RiskScore { get; init; }
        public bool ShouldBlock { get; init; }
        public bool RequiresReview { get; init; }
        public List<string> RiskIndicators { get; init; } = new();
        public string RecommendedAction { get; init; } = string.Empty;
    }

    public class PurchaseRateInfo
    {
        public int PurchasesLastMinute { get; init; }
        public int PurchasesLastHour { get; init; }
        public int PurchasesLastDay { get; init; }
        public DateTime LastPurchaseTime { get; init; }
        public bool IsRateLimited { get; init; }
        public TimeSpan RateLimitCooldown { get; init; }
    }
}