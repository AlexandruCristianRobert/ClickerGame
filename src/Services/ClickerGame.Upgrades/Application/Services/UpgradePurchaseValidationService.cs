using ClickerGame.Shared.Logging;
using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.Entities;
using ClickerGame.Upgrades.Domain.Enums;
using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClickerGame.Upgrades.Application.Services
{
    public class UpgradePurchaseValidationService : IUpgradePurchaseValidationService
    {
        private readonly UpgradesDbContext _context;
        private readonly ILogger<UpgradePurchaseValidationService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly IConfiguration _configuration;
        private readonly UpgradeValidationSettings _settings;

        public UpgradePurchaseValidationService(
            UpgradesDbContext context,
            ILogger<UpgradePurchaseValidationService> logger,
            ICorrelationService correlationService,
            IConfiguration configuration,
            IOptions<UpgradeValidationSettings> settings)
        {
            _context = context;
            _logger = logger;
            _correlationService = correlationService;
            _configuration = configuration;
            _settings = settings.Value;
        }

        public async Task<ValidationResult> ValidatePurchaseAsync(
            Guid playerId,
            PurchaseUpgradeRequest request,
            PlayerUpgradeContext playerContext,
            Upgrade upgrade,
            PlayerUpgrade? existingPlayerUpgrade = null)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var validationData = new Dictionary<string, object>();

            try
            {
                // 1. Basic Request Validation
                var requestValidation = ValidateRequest(request);
                if (!requestValidation.IsValid)
                {
                    errors.AddRange(requestValidation.Errors);
                }

                // 2. Player Context Validation
                var playerValidation = await ValidatePlayerContextAsync(playerId, playerContext);
                if (!playerValidation.IsValid)
                {
                    errors.AddRange(playerValidation.Errors);
                }

                // 3. Upgrade Existence and Status Validation
                var upgradeValidation = ValidateUpgradeStatus(upgrade);
                if (!upgradeValidation.IsValid)
                {
                    errors.AddRange(upgradeValidation.Errors);
                }

                // 4. Currency/Score Validation
                var currencyValidation = await ValidateCurrencyAsync(playerId, request, playerContext, upgrade, existingPlayerUpgrade);
                if (!currencyValidation.IsValid)
                {
                    errors.AddRange(currencyValidation.Errors);
                }
                validationData["currency"] = currencyValidation.ValidationData;

                // 5. Prerequisite Validation
                var prerequisiteValidation = await ValidatePrerequisitesAsync(upgrade, playerContext);
                if (!prerequisiteValidation.AllMet)
                {
                    errors.AddRange(prerequisiteValidation.UnmetPrerequisites);
                }
                validationData["prerequisites"] = prerequisiteValidation;

                // 6. Upgrade Limit Validation
                var limitValidation = await ValidateUpgradeLimitsAsync(upgrade, existingPlayerUpgrade, request.LevelsToPurchase);
                if (!limitValidation.WithinLimits)
                {
                    errors.Add($"Cannot purchase {request.LevelsToPurchase} levels. {limitValidation.LimitReason}");
                }
                validationData["limits"] = limitValidation;

                // 7. Anti-Fraud Checks
                var fraudValidation = await PerformAntiFraudChecksAsync(playerId, request, playerContext);
                if (fraudValidation.ShouldBlock)
                {
                    errors.Add("Purchase blocked due to suspicious activity");
                    _logger.LogWarning("Purchase blocked for player {PlayerId} due to fraud detection: {RiskFactors}",
                        playerId, string.Join(", ", fraudValidation.RiskFactors));
                }
                validationData["fraud"] = fraudValidation;

                // 8. Duplicate Purchase Prevention
                var duplicateCheck = await CheckForDuplicatePurchaseAsync(playerId, request.UpgradeId, request.LevelsToPurchase);
                if (duplicateCheck)
                {
                    errors.Add("Duplicate purchase detected. Please wait before making another purchase.");
                }

                // 9. Rate Limiting Check
                var rateLimitCheck = await CheckPurchaseRateLimitAsync(playerId);
                if (!rateLimitCheck.allowed)
                {
                    errors.Add($"Purchase rate limit exceeded. Please wait {rateLimitCheck.waitTime} before purchasing again.");
                }

                return new ValidationResult
                {
                    IsValid = errors.Count == 0,
                    Errors = errors,
                    Warnings = warnings,
                    ValidationData = validationData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error during purchase validation for player {PlayerId}", playerId);
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Validation error occurred. Please try again." }
                };
            }
        }

        public async Task<ValidationResult> ValidateBulkPurchaseAsync(
            Guid playerId,
            BulkPurchaseRequest request,
            PlayerUpgradeContext playerContext)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate bulk purchase limits
            if (request.Purchases.Count > _settings.MaxBulkPurchaseCount)
            {
                errors.Add($"Bulk purchase limit exceeded. Maximum {_settings.MaxBulkPurchaseCount} items allowed.");
            }

            // Validate total spending limit
            if (request.MaxTotalSpend > playerContext.CurrentScore)
            {
                errors.Add("Bulk purchase budget exceeds available score.");
            }

            // Check for duplicate upgrades in the same bulk request
            var duplicateUpgrades = request.Purchases
                .GroupBy(p => p.UpgradeId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateUpgrades.Any())
            {
                errors.Add($"Duplicate upgrades in bulk purchase: {string.Join(", ", duplicateUpgrades)}");
            }

            // Validate individual purchases
            foreach (var purchase in request.Purchases)
            {
                var upgrade = await _context.Upgrades.FirstOrDefaultAsync(u => u.UpgradeId == purchase.UpgradeId);
                if (upgrade == null)
                {
                    errors.Add($"Upgrade {purchase.UpgradeId} not found.");
                    continue;
                }

                var existingPlayerUpgrade = await _context.PlayerUpgrades
                    .FirstOrDefaultAsync(pu => pu.PlayerId == playerId && pu.UpgradeId == purchase.UpgradeId);

                var individualValidation = await ValidatePurchaseAsync(playerId, purchase, playerContext, upgrade, existingPlayerUpgrade);
                if (!individualValidation.IsValid)
                {
                    warnings.AddRange(individualValidation.Errors.Select(e => $"{purchase.UpgradeId}: {e}"));
                }
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            };
        }

        public async Task<AntiFraudResult> PerformAntiFraudChecksAsync(
            Guid playerId,
            PurchaseUpgradeRequest request,
            PlayerUpgradeContext playerContext)
        {
            var riskFactors = new List<string>();
            var riskScore = 0m;

            try
            {
                // 1. Rapid Purchase Detection
                var recentPurchases = await GetRecentPurchaseCountAsync(playerId, TimeSpan.FromMinutes(5));
                if (recentPurchases > _settings.MaxPurchasesPerTimeWindow)
                {
                    riskFactors.Add("Rapid purchasing pattern detected");
                    riskScore += 0.3m;
                }

                // 2. Unusual Purchase Amount Detection
                var averagePurchaseAmount = await GetAveragePurchaseAmountAsync(playerId);
                var upgrade = await _context.Upgrades.FirstOrDefaultAsync(u => u.UpgradeId == request.UpgradeId);
                if (upgrade != null)
                {
                    var existingPlayerUpgrade = await _context.PlayerUpgrades
                        .FirstOrDefaultAsync(pu => pu.PlayerId == playerId && pu.UpgradeId == request.UpgradeId);
                    var currentLevel = existingPlayerUpgrade?.Level ?? 0;
                    var estimatedCost = upgrade.Cost.CalculateTotalCostForLevels(currentLevel, currentLevel + request.LevelsToPurchase);

                    if (averagePurchaseAmount > BigNumber.Zero && estimatedCost > averagePurchaseAmount * 10)
                    {
                        riskFactors.Add("Purchase amount significantly higher than average");
                        riskScore += 0.2m;
                    }
                }

                // 3. Score Consistency Check
                if (playerContext.CurrentScore < BigNumber.Zero)
                {
                    riskFactors.Add("Negative player score detected");
                    riskScore += 0.8m;
                }

                // 4. Progression Logic Check
                var playerLevel = playerContext.PlayerLevel;
                if (playerLevel < _settings.MinLevelForHighValuePurchases && request.LevelsToPurchase > _settings.HighValuePurchaseThreshold)
                {
                    riskFactors.Add("High-value purchase from low-level player");
                    riskScore += 0.4m;
                }

                // 5. Time-based Pattern Analysis
                var purchaseTimePattern = await AnalyzePurchaseTimePatternAsync(playerId);
                if (purchaseTimePattern.isSuspicious)
                {
                    riskFactors.Add("Suspicious purchase timing pattern");
                    riskScore += 0.3m;
                }

                // 6. IP/Device Tracking (if available)
                // This would integrate with user session data

                var shouldBlock = riskScore >= _settings.FraudBlockThreshold;
                var isSuspicious = riskScore >= _settings.FraudSuspiciousThreshold;

                if (shouldBlock || isSuspicious)
                {
                    _logger.LogWarning("Anti-fraud check for player {PlayerId}: RiskScore={RiskScore}, Factors={RiskFactors}",
                        playerId, riskScore, string.Join(", ", riskFactors));
                }

                return new AntiFraudResult
                {
                    IsSuspicious = isSuspicious,
                    RiskScore = riskScore,
                    RiskFactors = riskFactors,
                    ShouldBlock = shouldBlock,
                    RecommendedAction = shouldBlock ? "Block purchase" :
                                      isSuspicious ? "Flag for review" : "Allow"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error during anti-fraud checks for player {PlayerId}", playerId);
                return new AntiFraudResult
                {
                    IsSuspicious = true,
                    RiskScore = 0.5m,
                    RiskFactors = new List<string> { "Error during fraud check" },
                    ShouldBlock = false,
                    RecommendedAction = "Allow with monitoring"
                };
            }
        }

        public async Task<bool> CheckForDuplicatePurchaseAsync(Guid playerId, string upgradeId, int requestedLevels)
        {
            try
            {
                // Check for exact duplicate purchases within the last 30 seconds
                var cutoffTime = DateTime.UtcNow.AddSeconds(-30);

                var recentPurchase = await _context.PlayerUpgrades
                    .Where(pu => pu.PlayerId == playerId &&
                                pu.UpgradeId == upgradeId &&
                                pu.LastUpgradedAt >= cutoffTime)
                    .OrderByDescending(pu => pu.LastUpgradedAt)
                    .FirstOrDefaultAsync();

                return recentPurchase != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error checking for duplicate purchase");
                return false; // Allow purchase if check fails
            }
        }

        public async Task<PrerequisiteValidationResult> ValidatePrerequisitesAsync(
            Upgrade upgrade,
            PlayerUpgradeContext playerContext)
        {
            var unmetPrerequisites = new List<string>();
            var missingUpgrades = new List<string>();
            var prerequisiteDetails = new Dictionary<string, object>();

            foreach (var prerequisite in upgrade.Prerequisites)
            {
                var isSatisfied = prerequisite.IsSatisfied(
                    playerContext.PlayerLevel,
                    playerContext.CurrentScore,
                    playerContext.ClickCount,
                    playerContext.OwnedUpgrades);

                prerequisiteDetails[prerequisite.Type.ToString()] = new
                {
                    Required = prerequisite.RequiredValue.ToString(),
                    Current = GetCurrentValueForPrerequisite(prerequisite, playerContext),
                    IsSatisfied = isSatisfied
                };

                if (!isSatisfied)
                {
                    unmetPrerequisites.Add(prerequisite.Description);

                    if (prerequisite.Type == PrerequisiteType.OtherUpgrade &&
                        !string.IsNullOrEmpty(prerequisite.RequiredUpgradeId))
                    {
                        missingUpgrades.Add(prerequisite.RequiredUpgradeId);
                    }
                }
            }

            return new PrerequisiteValidationResult
            {
                AllMet = unmetPrerequisites.Count == 0,
                UnmetPrerequisites = unmetPrerequisites,
                MissingUpgrades = missingUpgrades,
                PrerequisiteDetails = prerequisiteDetails
            };
        }

        public async Task<UpgradeLimitResult> ValidateUpgradeLimitsAsync(
            Upgrade upgrade,
            PlayerUpgrade? existingPlayerUpgrade,
            int requestedLevels)
        {
            var currentLevel = existingPlayerUpgrade?.Level ?? 0;
            var maxAllowedLevels = upgrade.MaxLevel - currentLevel;
            var withinLimits = requestedLevels <= maxAllowedLevels;

            string? limitReason = null;
            if (!withinLimits)
            {
                if (currentLevel >= upgrade.MaxLevel)
                {
                    limitReason = "Upgrade is already at maximum level";
                }
                else
                {
                    limitReason = $"Can only purchase {maxAllowedLevels} more levels (current: {currentLevel}, max: {upgrade.MaxLevel})";
                }
            }

            return new UpgradeLimitResult
            {
                WithinLimits = withinLimits,
                MaxAllowedLevels = maxAllowedLevels,
                RequestedLevels = requestedLevels,
                CurrentLevel = currentLevel,
                LimitReason = limitReason
            };
        }

        // Private helper methods
        private ValidationResult ValidateRequest(PurchaseUpgradeRequest request)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(request.UpgradeId))
            {
                errors.Add("Upgrade ID is required");
            }

            if (request.LevelsToPurchase <= 0)
            {
                errors.Add("Levels to purchase must be greater than 0");
            }

            if (request.LevelsToPurchase > _settings.MaxLevelsPerPurchase)
            {
                errors.Add($"Cannot purchase more than {_settings.MaxLevelsPerPurchase} levels at once");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private async Task<ValidationResult> ValidatePlayerContextAsync(Guid playerId, PlayerUpgradeContext playerContext)
        {
            var errors = new List<string>();

            if (playerContext.PlayerId != playerId)
            {
                errors.Add("Player context mismatch");
            }

            if (playerContext.CurrentScore < BigNumber.Zero)
            {
                errors.Add("Invalid player score");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private ValidationResult ValidateUpgradeStatus(Upgrade upgrade)
        {
            var errors = new List<string>();

            if (!upgrade.IsActive)
            {
                errors.Add("Upgrade is not currently active");
            }

            if (upgrade.IsHidden)
            {
                errors.Add("Upgrade is not available for purchase");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private async Task<ValidationResult> ValidateCurrencyAsync(
            Guid playerId,
            PurchaseUpgradeRequest request,
            PlayerUpgradeContext playerContext,
            Upgrade upgrade,
            PlayerUpgrade? existingPlayerUpgrade)
        {
            var errors = new List<string>();
            var validationData = new Dictionary<string, object>();

            try
            {
                var currentLevel = existingPlayerUpgrade?.Level ?? 0;
                var estimatedCost = upgrade.Cost.CalculateTotalCostForLevels(currentLevel, currentLevel + request.LevelsToPurchase);

                validationData["estimatedCost"] = estimatedCost.ToString();
                validationData["currentScore"] = playerContext.CurrentScore.ToString();
                validationData["canAfford"] = playerContext.CurrentScore >= estimatedCost;

                if (playerContext.CurrentScore < estimatedCost)
                {
                    errors.Add($"Insufficient score. Required: {estimatedCost}, Available: {playerContext.CurrentScore}");
                }

                // Check for reasonable purchase amounts
                if (estimatedCost > playerContext.CurrentScore * 2)
                {
                    errors.Add("Purchase amount exceeds reasonable limits");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error validating currency for player {PlayerId}", playerId);
                errors.Add("Error calculating purchase cost");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                ValidationData = validationData
            };
        }

        private async Task<int> GetRecentPurchaseCountAsync(Guid playerId, TimeSpan timeWindow)
        {
            var cutoffTime = DateTime.UtcNow - timeWindow;
            return await _context.PlayerUpgrades
                .Where(pu => pu.PlayerId == playerId && pu.LastUpgradedAt >= cutoffTime)
                .CountAsync();
        }

        private async Task<BigNumber> GetAveragePurchaseAmountAsync(Guid playerId)
        {
            // This would require a purchase history table to implement properly
            // For now, return a reasonable default
            await Task.CompletedTask;
            return new BigNumber(1000);
        }

        private async Task<(bool isSuspicious, string reason)> AnalyzePurchaseTimePatternAsync(Guid playerId)
        {
            // Analyze purchase timing patterns
            var recentPurchases = await _context.PlayerUpgrades
                .Where(pu => pu.PlayerId == playerId)
                .OrderByDescending(pu => pu.LastUpgradedAt)
                .Take(10)
                .ToListAsync();

            if (recentPurchases.Count < 3)
            {
                return (false, "Insufficient data");
            }

            // Check for bot-like regular intervals
            var intervals = new List<double>();
            for (int i = 0; i < recentPurchases.Count - 1; i++)
            {
                var interval = (recentPurchases[i].LastUpgradedAt - recentPurchases[i + 1].LastUpgradedAt).TotalSeconds;
                intervals.Add(interval);
            }

            if (intervals.Count >= 3)
            {
                var averageInterval = intervals.Average();
                var isRegular = intervals.All(i => Math.Abs(i - averageInterval) < 2); // Within 2 seconds

                if (isRegular && averageInterval < 10)
                {
                    return (true, "Regular short intervals detected");
                }
            }

            return (false, "Normal pattern");
        }

        private async Task<(bool allowed, TimeSpan waitTime)> CheckPurchaseRateLimitAsync(Guid playerId)
        {
            var recentPurchases = await GetRecentPurchaseCountAsync(playerId, TimeSpan.FromMinutes(1));

            if (recentPurchases >= _settings.MaxPurchasesPerMinute)
            {
                return (false, TimeSpan.FromMinutes(1));
            }

            return (true, TimeSpan.Zero);
        }

        private string GetCurrentValueForPrerequisite(UpgradePrerequisite prerequisite, PlayerUpgradeContext playerContext)
        {
            return prerequisite.Type switch
            {
                PrerequisiteType.PlayerLevel => playerContext.PlayerLevel.ToString(),
                PrerequisiteType.TotalScore => playerContext.CurrentScore.ToString(),
                PrerequisiteType.ClickCount => playerContext.ClickCount.ToString(),
                PrerequisiteType.OtherUpgrade => prerequisite.RequiredUpgradeId != null
                    ? playerContext.OwnedUpgrades.GetValueOrDefault(prerequisite.RequiredUpgradeId, 0).ToString()
                    : "0",
                _ => "Unknown"
            };
        }
    }

    // Configuration class for validation settings
    public class UpgradeValidationSettings
    {
        public int MaxLevelsPerPurchase { get; set; } = 100;
        public int MaxBulkPurchaseCount { get; set; } = 10;
        public int MaxPurchasesPerTimeWindow { get; set; } = 10;
        public int MaxPurchasesPerMinute { get; set; } = 5;
        public int MinLevelForHighValuePurchases { get; set; } = 10;
        public int HighValuePurchaseThreshold { get; set; } = 50;
        public decimal FraudSuspiciousThreshold { get; set; } = 0.5m;
        public decimal FraudBlockThreshold { get; set; } = 0.8m;
    }
}