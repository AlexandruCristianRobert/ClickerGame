using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.Entities;
using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.Services
{
    public interface IUpgradePurchaseValidationService
    {
        Task<ValidationResult> ValidatePurchaseAsync(
            Guid playerId,
            PurchaseUpgradeRequest request,
            PlayerUpgradeContext playerContext,
            Upgrade upgrade,
            PlayerUpgrade? existingPlayerUpgrade = null);

        Task<ValidationResult> ValidateBulkPurchaseAsync(
            Guid playerId,
            BulkPurchaseRequest request,
            PlayerUpgradeContext playerContext);

        Task<AntiFraudResult> PerformAntiFraudChecksAsync(
            Guid playerId,
            PurchaseUpgradeRequest request,
            PlayerUpgradeContext playerContext);

        Task<bool> CheckForDuplicatePurchaseAsync(
            Guid playerId,
            string upgradeId,
            int requestedLevels);

        Task<PrerequisiteValidationResult> ValidatePrerequisitesAsync(
            Upgrade upgrade,
            PlayerUpgradeContext playerContext);

        Task<UpgradeLimitResult> ValidateUpgradeLimitsAsync(
            Upgrade upgrade,
            PlayerUpgrade? existingPlayerUpgrade,
            int requestedLevels);
    }

    public class ValidationResult
    {
        public bool IsValid { get; init; }
        public List<string> Errors { get; init; } = new();
        public List<string> Warnings { get; init; } = new();
        public Dictionary<string, object> ValidationData { get; init; } = new();
    }

    public class AntiFraudResult
    {
        public bool IsSuspicious { get; init; }
        public decimal RiskScore { get; init; }
        public List<string> RiskFactors { get; init; } = new();
        public bool ShouldBlock { get; init; }
        public string? RecommendedAction { get; init; }
    }

    public class PrerequisiteValidationResult
    {
        public bool AllMet { get; init; }
        public List<string> UnmetPrerequisites { get; init; } = new();
        public List<string> MissingUpgrades { get; init; } = new();
        public Dictionary<string, object> PrerequisiteDetails { get; init; } = new();
    }

    public class UpgradeLimitResult
    {
        public bool WithinLimits { get; init; }
        public int MaxAllowedLevels { get; init; }
        public int RequestedLevels { get; init; }
        public int CurrentLevel { get; init; }
        public string? LimitReason { get; init; }
    }
}