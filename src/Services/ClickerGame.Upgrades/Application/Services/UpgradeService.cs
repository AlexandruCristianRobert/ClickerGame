using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.Entities;
using ClickerGame.Upgrades.Domain.Events;
using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Domain.Enums;
using ClickerGame.Upgrades.Infrastructure.Data;
using ClickerGame.Shared.Logging;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace ClickerGame.Upgrades.Application.Services
{
    public class UpgradeService : IUpgradeService
    {
        private readonly UpgradesDbContext _context;
        private readonly IUpgradeCalculationEngine _calculationEngine;
        private readonly IPlayerContextService _playerContextService;
        private readonly IMediator _mediator;
        private readonly ILogger<UpgradeService> _logger;
        private readonly ICorrelationService _correlationService;

        public UpgradeService(
            UpgradesDbContext context,
            IUpgradeCalculationEngine calculationEngine,
            IPlayerContextService playerContextService,
            IMediator mediator,
            ILogger<UpgradeService> logger,
            ICorrelationService correlationService)
        {
            _context = context;
            _calculationEngine = calculationEngine;
            _playerContextService = playerContextService;
            _mediator = mediator;
            _logger = logger;
            _correlationService = correlationService;
        }

        public async Task<UpgradeDto> GetUpgradeAsync(string upgradeId)
        {
            var upgrade = await _context.Upgrades
                .FirstOrDefaultAsync(u => u.UpgradeId == upgradeId);

            if (upgrade == null)
            {
                throw new InvalidOperationException($"Upgrade {upgradeId} not found");
            }

            return MapToUpgradeDto(upgrade, 0, false);
        }

        public async Task<IEnumerable<UpgradeDto>> GetAvailableUpgradesAsync(Guid playerId)
        {
            _logger.LogRequestStart(_correlationService, "GetAvailableUpgrades");

            var playerContext = await _playerContextService.GetPlayerContextAsync(playerId);

            var upgrades = await _context.Upgrades
                .Where(u => u.IsActive && !u.IsHidden)
                .ToListAsync();

            var upgradeDtos = new List<UpgradeDto>();

            foreach (var upgrade in upgrades)
            {
                var currentLevel = playerContext.OwnedUpgrades.GetValueOrDefault(upgrade.UpgradeId, 0);
                var canPurchase = _calculationEngine.CanPlayerPurchaseUpgrade(upgrade, playerContext);
                var upgradeDto = MapToUpgradeDto(upgrade, currentLevel, canPurchase);

                // Add cost and effect information
                if (currentLevel < upgrade.MaxLevel)
                {
                    upgradeDto = new UpgradeDto
                    {
                        UpgradeId = upgradeDto.UpgradeId,
                        Name = upgradeDto.Name,
                        Description = upgradeDto.Description,
                        Category = upgradeDto.Category,
                        Rarity = upgradeDto.Rarity,
                        IconUrl = upgradeDto.IconUrl,
                        MaxLevel = upgradeDto.MaxLevel,
                        IsActive = upgradeDto.IsActive,
                        IsHidden = upgradeDto.IsHidden,
                        CreatedAt = upgradeDto.CreatedAt,
                        CurrentLevel = upgradeDto.CurrentLevel,
                        CanPurchase = upgradeDto.CanPurchase,
                        Effects = upgradeDto.Effects,
                        Prerequisites = upgradeDto.Prerequisites,
                        PrerequisitesMet = upgradeDto.PrerequisitesMet,
                        PrerequisiteWarnings = upgradeDto.PrerequisiteWarnings,
                        NextLevelCost = _calculationEngine.CalculateUpgradeCost(upgrade, currentLevel, 1),
                        NextLevelEffect = CalculateNextLevelTotalEffect(upgrade, currentLevel)
                    };
                }

                upgradeDtos.Add(upgradeDto);
            }

            _logger.LogBusinessEvent(_correlationService, "AvailableUpgradesRetrieved",
                new { PlayerId = playerId, UpgradeCount = upgradeDtos.Count });

            return upgradeDtos.OrderBy(u => u.Category).ThenBy(u => u.Rarity);
        }

        public async Task<IEnumerable<UpgradeDto>> GetUpgradesByCategoryAsync(UpgradeCategory category, Guid playerId)
        {
            var allUpgrades = await GetAvailableUpgradesAsync(playerId);
            return allUpgrades.Where(u => u.Category == category);
        }

        public async Task<UpgradePurchaseResult> PurchaseUpgradeAsync(Guid playerId, PurchaseUpgradeRequest request)
        {
            _logger.LogRequestStart(_correlationService, "PurchaseUpgrade");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate player and get context
                var playerContext = await _playerContextService.GetPlayerContextAsync(playerId);

                // Get upgrade
                var upgrade = await _context.Upgrades
                    .FirstOrDefaultAsync(u => u.UpgradeId == request.UpgradeId);

                if (upgrade == null)
                {
                    return new UpgradePurchaseResult
                    {
                        Success = false,
                        Errors = new List<string> { $"Upgrade {request.UpgradeId} not found" }
                    };
                }

                // Get current player upgrade
                var playerUpgrade = await _context.PlayerUpgrades
                    .Include(pu => pu.Upgrade)
                    .FirstOrDefaultAsync(pu => pu.PlayerId == playerId && pu.UpgradeId == request.UpgradeId);

                var currentLevel = playerUpgrade?.Level ?? 0;
                var levelsToPurchase = Math.Min(request.LevelsToPurchase, upgrade.MaxLevel - currentLevel);

                // Validate purchase
                if (!_calculationEngine.CanPlayerPurchaseUpgrade(upgrade, playerContext))
                {
                    return new UpgradePurchaseResult
                    {
                        Success = false,
                        UpgradeId = request.UpgradeId,
                        Errors = new List<string> { "Cannot purchase this upgrade at this time" }
                    };
                }

                // Calculate cost
                var totalCost = _calculationEngine.CalculateUpgradeCost(upgrade, currentLevel, levelsToPurchase);

                if (request.MaxSpendAmount > BigNumber.Zero && totalCost > request.MaxSpendAmount)
                {
                    // Recalculate based on budget
                    var bulkResult = _calculationEngine.CalculateBulkUpgrade(upgrade, playerContext, request.MaxSpendAmount);
                    levelsToPurchase = bulkResult.LevelsCanAfford;
                    totalCost = bulkResult.TotalCost;
                }

                if (levelsToPurchase <= 0 || totalCost > playerContext.CurrentScore)
                {
                    return new UpgradePurchaseResult
                    {
                        Success = false,
                        UpgradeId = request.UpgradeId,
                        Errors = new List<string> { "Insufficient score to purchase upgrade" }
                    };
                }

                // Deduct cost
                if (!await _playerContextService.DeductPlayerScoreAsync(playerId, totalCost))
                {
                    return new UpgradePurchaseResult
                    {
                        Success = false,
                        UpgradeId = request.UpgradeId,
                        Errors = new List<string> { "Failed to deduct score - please try again" }
                    };
                }

                // Update or create player upgrade
                if (playerUpgrade == null)
                {
                    playerUpgrade = new PlayerUpgrade
                    {
                        PlayerId = playerId,
                        UpgradeId = request.UpgradeId,
                        Level = levelsToPurchase,
                        Upgrade = upgrade
                    };
                    _context.PlayerUpgrades.Add(playerUpgrade);
                }
                else
                {
                    playerUpgrade.UpgradeLevel(levelsToPurchase);
                }

                await _context.SaveChangesAsync();

                // Calculate updated effects
                var updatedEffects = await CalculatePlayerEffectsAsync(playerId);

                // Publish events
                await PublishUpgradePurchasedEvent(playerId, request.UpgradeId,
                    currentLevel + levelsToPurchase, levelsToPurchase, totalCost);
                await PublishPlayerEffectsUpdatedEvent(playerId, updatedEffects);

                await transaction.CommitAsync();

                var result = new UpgradePurchaseResult
                {
                    Success = true,
                    UpgradeId = request.UpgradeId,
                    LevelsPurchased = levelsToPurchase,
                    NewLevel = currentLevel + levelsToPurchase,
                    TotalCostPaid = totalCost,
                    RemainingScore = playerContext.CurrentScore - totalCost,
                    UpdatedEffects = updatedEffects,
                    Messages = new List<string>
                    {
                        $"Successfully purchased {levelsToPurchase} level(s) of {upgrade.Name}"
                    }
                };

                _logger.LogBusinessEvent(_correlationService, "UpgradePurchaseSuccess",
                    new { PlayerId = playerId, UpgradeId = request.UpgradeId, LevelsPurchased = levelsToPurchase, Cost = totalCost.ToString() });

                await transaction.CommitAsync();

                try
                {
                    await _playerContextService.ApplyUpgradeEffectsToGameCoreAsync(playerId, updatedEffects, request.UpgradeId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(_correlationService, ex, "Failed to apply upgrade effects to GameCore for player {PlayerId}, upgrade {UpgradeId}", 
                        playerId, request.UpgradeId);
                }

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(_correlationService, ex, "Error purchasing upgrade {UpgradeId} for player {PlayerId}",
                    request.UpgradeId, playerId);

                return new UpgradePurchaseResult
                {
                    Success = false,
                    UpgradeId = request.UpgradeId,
                    Errors = new List<string> { "An error occurred during purchase" }
                };
            }
        }

        public async Task<BulkUpgradePurchaseResult> PurchaseUpgradeBulkAsync(Guid playerId, BulkPurchaseRequest request)
        {
            _logger.LogRequestStart(_correlationService, "PurchaseUpgradeBulk");

            var results = new List<UpgradePurchaseResult>();
            var totalSpent = BigNumber.Zero;
            var successCount = 0;
            var failCount = 0;

            foreach (var purchaseRequest in request.Purchases)
            {
                if (totalSpent >= request.MaxTotalSpend)
                {
                    results.Add(new UpgradePurchaseResult
                    {
                        Success = false,
                        UpgradeId = purchaseRequest.UpgradeId,
                        Errors = new List<string> { "Budget limit reached" }
                    });
                    failCount++;
                    continue;
                }

                // Adjust request to respect remaining budget
                var remainingBudget = request.MaxTotalSpend - totalSpent;
                var adjustedRequest = new PurchaseUpgradeRequest
                {
                    UpgradeId = purchaseRequest.UpgradeId,
                    LevelsToPurchase = purchaseRequest.LevelsToPurchase,
                    ConfirmPurchase = purchaseRequest.ConfirmPurchase,
                    AutoPurchasePrerequisites = purchaseRequest.AutoPurchasePrerequisites,
                    MaxSpendAmount = remainingBudget
                };

                var result = await PurchaseUpgradeAsync(playerId, adjustedRequest);
                results.Add(result);

                if (result.Success)
                {
                    totalSpent = totalSpent + result.TotalCostPaid;
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }

            var finalEffects = await CalculatePlayerEffectsAsync(playerId);
            var remainingScore = await _playerContextService.GetPlayerScoreAsync(playerId);

            _logger.LogBusinessEvent(_correlationService, "BulkUpgradePurchaseCompleted",
                new { PlayerId = playerId, Successful = successCount, Failed = failCount, TotalSpent = totalSpent.ToString() });

            return new BulkUpgradePurchaseResult
            {
                OverallSuccess = successCount > 0,
                PurchaseResults = results,
                TotalSpent = totalSpent,
                RemainingScore = remainingScore,
                FinalEffects = finalEffects,
                SuccessfulPurchases = successCount,
                FailedPurchases = failCount,
                OverallMessages = new List<string>
                {
                    $"Completed {successCount} successful purchases, {failCount} failed"
                }
            };
        }

        public async Task<UpgradePreview> PreviewUpgradePurchaseAsync(Guid playerId, PreviewUpgradeRequest request)
        {
            var playerContext = await _playerContextService.GetPlayerContextAsync(playerId);
            var upgrade = await _context.Upgrades
                .FirstOrDefaultAsync(u => u.UpgradeId == request.UpgradeId);

            if (upgrade == null)
            {
                throw new InvalidOperationException($"Upgrade {request.UpgradeId} not found");
            }

            return _calculationEngine.PreviewUpgradeEffects(upgrade, playerContext, request.LevelsToPurchase);
        }

        public async Task<PlayerUpgradeProgressDto> GetPlayerUpgradeProgressAsync(Guid playerId)
        {
            var playerUpgrades = await _context.PlayerUpgrades
                .Include(pu => pu.Upgrade)
                .Where(pu => pu.PlayerId == playerId)
                .ToListAsync();

            var effects = await CalculatePlayerEffectsAsync(playerId);
            var totalSpent = BigNumber.Zero; // Would need to calculate from purchase history

            var upgradesByCategory = playerUpgrades
                .GroupBy(pu => pu.Upgrade.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            var upgradesByRarity = playerUpgrades
                .GroupBy(pu => pu.Upgrade.Rarity)
                .ToDictionary(g => g.Key, g => g.Count());

            var availableUpgrades = await _context.Upgrades
                .CountAsync(u => u.IsActive && !u.IsHidden);

            return new PlayerUpgradeProgressDto
            {
                PlayerId = playerId,
                TotalUpgradesOwned = playerUpgrades.Count,
                TotalUpgradeLevels = playerUpgrades.Sum(pu => pu.Level),
                TotalSpent = totalSpent,
                UpgradesByCategory = upgradesByCategory,
                UpgradesByRarity = upgradesByRarity,
                CurrentEffects = effects,
                UnlockedUpgrades = playerUpgrades.Count,
                AvailableUpgrades = availableUpgrades,
                RecentPurchases = playerUpgrades
                    .OrderByDescending(pu => pu.LastUpgradedAt)
                    .Take(5)
                    .Select(MapToPlayerUpgradeDto)
                    .ToList(),
                LastPurchaseAt = playerUpgrades.Any() ?
                    playerUpgrades.Max(pu => pu.LastUpgradedAt) : DateTime.MinValue
            };
        }

        public async Task<PlayerEffectSummary> CalculatePlayerEffectsAsync(Guid playerId)
        {
            var playerUpgrades = await _context.PlayerUpgrades
                .Include(pu => pu.Upgrade)
                .Where(pu => pu.PlayerId == playerId)
                .ToListAsync();

            return _calculationEngine.CalculatePlayerEffects(playerUpgrades);
        }

        public async Task<IEnumerable<PlayerUpgradeDto>> GetPlayerUpgradesAsync(Guid playerId)
        {
            var playerUpgrades = await _context.PlayerUpgrades
                .Include(pu => pu.Upgrade)
                .Where(pu => pu.PlayerId == playerId)
                .ToListAsync();

            return playerUpgrades.Select(MapToPlayerUpgradeDto);
        }

        public async Task<UpgradeRecommendation> GetUpgradeRecommendationAsync(Guid playerId, BigNumber budget)
        {
            var playerContext = await _playerContextService.GetPlayerContextAsync(playerId);
            var availableUpgrades = await _context.Upgrades
                .Where(u => u.IsActive && !u.IsHidden)
                .ToListAsync();

            return _calculationEngine.GetBestUpgradeForBudget(availableUpgrades, playerContext, budget);
        }

        public async Task<IEnumerable<UpgradeRecommendation>> GetTopUpgradeRecommendationsAsync(Guid playerId, int count = 5)
        {
            var playerContext = await _playerContextService.GetPlayerContextAsync(playerId);
            var availableUpgrades = await _context.Upgrades
                .Where(u => u.IsActive && !u.IsHidden)
                .ToListAsync();

            var recommendations = new List<UpgradeRecommendation>();

            foreach (var upgrade in availableUpgrades)
            {
                if (_calculationEngine.CanPlayerPurchaseUpgrade(upgrade, playerContext))
                {
                    var currentLevel = playerContext.OwnedUpgrades.GetValueOrDefault(upgrade.UpgradeId, 0);
                    var efficiency = _calculationEngine.CalculateUpgradeEfficiency(upgrade, currentLevel, playerContext);
                    var cost = _calculationEngine.CalculateUpgradeCost(upgrade, currentLevel, 1);
                    var effect = CalculateNextLevelTotalEffect(upgrade, currentLevel);

                    recommendations.Add(new UpgradeRecommendation
                    {
                        UpgradeId = upgrade.UpgradeId,
                        UpgradeName = upgrade.Name,
                        RecommendedLevels = 1,
                        TotalCost = cost,
                        EfficiencyScore = efficiency,
                        ExpectedEffectIncrease = effect,
                        Reasoning = $"Efficiency score: {efficiency:F2}"
                    });
                }
            }

            return recommendations
                .OrderByDescending(r => r.EfficiencyScore)
                .Take(count);
        }

        public Task<UpgradeEfficiencyAnalysis> AnalyzeUpgradeEfficiencyAsync(Guid playerId)
        {
            // Implementation for efficiency analysis
            return Task.FromResult(new UpgradeEfficiencyAnalysis());
        }

        public Task<bool> UnlockUpgradeForPlayerAsync(Guid playerId, string upgradeId)
        {
            // Administrative function to unlock upgrades
            return Task.FromResult(true);
        }

        public async Task ResetPlayerUpgradesAsync(Guid playerId)
        {
            var playerUpgrades = await _context.PlayerUpgrades
                .Where(pu => pu.PlayerId == playerId)
                .ToListAsync();

            _context.PlayerUpgrades.RemoveRange(playerUpgrades);
            await _context.SaveChangesAsync();

            _logger.LogBusinessEvent(_correlationService, "PlayerUpgradesReset", new { PlayerId = playerId });
        }

        public async Task<UpgradeStatistics> GetUpgradeStatisticsAsync(string upgradeId)
        {
            var playerUpgrades = await _context.PlayerUpgrades
                .Where(pu => pu.UpgradeId == upgradeId)
                .ToListAsync();

            return new UpgradeStatistics
            {
                UpgradeId = upgradeId,
                TotalOwners = playerUpgrades.Count,
                AverageLevel = playerUpgrades.Any() ? playerUpgrades.Average(pu => pu.Level) : 0,
                MaxLevel = playerUpgrades.Any() ? playerUpgrades.Max(pu => pu.Level) : 0,
                TotalLevels = playerUpgrades.Sum(pu => pu.Level)
            };
        }

        // Private helper methods
        private async Task PublishUpgradePurchasedEvent(Guid playerId, string upgradeId, int newLevel, int levelsPurchased, BigNumber costPaid)
        {
            var eventData = new UpgradePurchasedEvent(playerId, upgradeId, newLevel, levelsPurchased, costPaid);
            await _mediator.Publish(eventData);
        }

        private async Task PublishPlayerEffectsUpdatedEvent(Guid playerId, PlayerEffectSummary effects)
        {
            var eventData = new PlayerEffectsUpdatedEvent(
                playerId,
                effects.UpgradeContributions,
                effects.TotalClickPowerBonus,
                effects.TotalPassiveIncomeBonus,
                effects.TotalMultiplier);
            await _mediator.Publish(eventData);
        }

        private BigNumber CalculateNextLevelTotalEffect(Upgrade upgrade, int currentLevel)
        {
            var totalEffect = BigNumber.Zero;
            foreach (var effect in upgrade.Effects)
            {
                var currentEffect = _calculationEngine.CalculateUpgradeEffect(effect, currentLevel);
                var nextLevelEffect = _calculationEngine.CalculateUpgradeEffect(effect, currentLevel + 1);
                totalEffect = totalEffect + (nextLevelEffect - currentEffect);
            }
            return totalEffect;
        }

        private UpgradeDto MapToUpgradeDto(Upgrade upgrade, int currentLevel, bool canPurchase)
        {
            return new UpgradeDto
            {
                UpgradeId = upgrade.UpgradeId,
                Name = upgrade.Name,
                Description = upgrade.Description,
                Category = upgrade.Category,
                Rarity = upgrade.Rarity,
                IconUrl = upgrade.IconUrl,
                MaxLevel = upgrade.MaxLevel,
                IsActive = upgrade.IsActive,
                IsHidden = upgrade.IsHidden,
                CreatedAt = upgrade.CreatedAt,
                CurrentLevel = currentLevel,
                CanPurchase = canPurchase,
                Effects = upgrade.Effects.Select(MapToEffectDto).ToList(),
                Prerequisites = upgrade.Prerequisites.Select(MapToPrerequisiteDto).ToList(),
                PrerequisitesMet = canPurchase
            };
        }

        private UpgradeEffectDto MapToEffectDto(UpgradeEffect effect)
        {
            return new UpgradeEffectDto
            {
                TargetCategory = effect.TargetCategory,
                EffectType = effect.EffectType,
                BaseValue = effect.BaseValue,
                ScalingFactor = effect.ScalingFactor,
                Description = effect.Description
            };
        }

        private UpgradePrerequisiteDto MapToPrerequisiteDto(UpgradePrerequisite prerequisite)
        {
            return new UpgradePrerequisiteDto
            {
                Type = prerequisite.Type,
                RequiredUpgradeId = prerequisite.RequiredUpgradeId,
                RequiredValue = prerequisite.RequiredValue,
                RequiredLevel = prerequisite.RequiredLevel,
                Description = prerequisite.Description
            };
        }

        private PlayerUpgradeDto MapToPlayerUpgradeDto(PlayerUpgrade playerUpgrade)
        {
            var currentEffect = BigNumber.Zero;
            foreach (var effect in playerUpgrade.Upgrade.Effects)
            {
                currentEffect = currentEffect + _calculationEngine.CalculateUpgradeEffect(effect, playerUpgrade.Level);
            }

            var nextLevelCost = BigNumber.Zero;
            if (playerUpgrade.Level < playerUpgrade.Upgrade.MaxLevel)
            {
                nextLevelCost = _calculationEngine.CalculateUpgradeCost(playerUpgrade.Upgrade, playerUpgrade.Level, 1);
            }

            return new PlayerUpgradeDto
            {
                PlayerUpgradeId = playerUpgrade.PlayerUpgradeId,
                UpgradeId = playerUpgrade.UpgradeId,
                UpgradeName = playerUpgrade.Upgrade.Name,
                Level = playerUpgrade.Level,
                CurrentEffect = currentEffect,
                PurchasedAt = playerUpgrade.PurchasedAt,
                LastUpgradedAt = playerUpgrade.LastUpgradedAt,
                CanUpgrade = playerUpgrade.CanUpgrade(),
                NextLevelCost = nextLevelCost
            };
        }
    }
}