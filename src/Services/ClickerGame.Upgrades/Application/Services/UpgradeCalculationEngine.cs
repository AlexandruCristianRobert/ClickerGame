using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.Entities;
using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Domain.Enums;

namespace ClickerGame.Upgrades.Application.Services
{
    public class UpgradeCalculationEngine : IUpgradeCalculationEngine
    {
        private readonly ILogger<UpgradeCalculationEngine> _logger;

        public UpgradeCalculationEngine(ILogger<UpgradeCalculationEngine> logger)
        {
            _logger = logger;
        }

        public BigNumber CalculateUpgradeCost(Upgrade upgrade, int currentLevel, int levelsToUpgrade = 1)
        {
            if (levelsToUpgrade <= 0) return BigNumber.Zero;
            if (currentLevel + levelsToUpgrade > upgrade.MaxLevel)
            {
                levelsToUpgrade = upgrade.MaxLevel - currentLevel;
            }

            return upgrade.Cost.CalculateTotalCostForLevels(currentLevel, currentLevel + levelsToUpgrade);
        }

        public BigNumber CalculateTotalCostForLevels(Upgrade upgrade, int fromLevel, int toLevel)
        {
            if (fromLevel >= toLevel || fromLevel < 0) return BigNumber.Zero;

            var cappedToLevel = Math.Min(toLevel, upgrade.MaxLevel);
            return upgrade.Cost.CalculateTotalCostForLevels(fromLevel, cappedToLevel);
        }

        public BigNumber CalculateUpgradeEffect(UpgradeEffect effect, int level)
        {
            if (level <= 0) return BigNumber.Zero;
            return effect.CalculateEffectAtLevel(level);
        }

        public PlayerEffectSummary CalculatePlayerEffects(IEnumerable<PlayerUpgrade> playerUpgrades)
        {
            var categoryEffects = new Dictionary<UpgradeCategory, BigNumber>();
            var upgradeContributions = new Dictionary<string, BigNumber>();
            var totalClickPower = BigNumber.Zero;
            var totalPassiveIncome = BigNumber.Zero;
            var totalMultiplier = 1.0m;
            var totalUpgradeLevel = 0;

            foreach (var playerUpgrade in playerUpgrades)
            {
                if (playerUpgrade.Upgrade == null) continue;

                totalUpgradeLevel += playerUpgrade.Level;
                var upgradeContribution = BigNumber.Zero;

                foreach (var effect in playerUpgrade.Upgrade.Effects)
                {
                    var effectValue = CalculateUpgradeEffect(effect, playerUpgrade.Level);
                    upgradeContribution = upgradeContribution + effectValue;

                    // Accumulate by category
                    if (!categoryEffects.ContainsKey(effect.TargetCategory))
                        categoryEffects[effect.TargetCategory] = BigNumber.Zero;

                    categoryEffects[effect.TargetCategory] = categoryEffects[effect.TargetCategory] + effectValue;

                    // Calculate specific bonuses
                    switch (effect.TargetCategory)
                    {
                        case UpgradeCategory.ClickPower:
                            totalClickPower = totalClickPower + effectValue;
                            break;
                        case UpgradeCategory.PassiveIncome:
                            totalPassiveIncome = totalPassiveIncome + effectValue;
                            break;
                        case UpgradeCategory.Multipliers:
                            if (effect.EffectType == UpgradeType.Percentage)
                            {
                                totalMultiplier += effectValue.ToDecimal() / 100m;
                            }
                            else
                            {
                                totalMultiplier *= effectValue.ToDecimal();
                            }
                            break;
                    }
                }

                upgradeContributions[playerUpgrade.UpgradeId] = upgradeContribution;
            }

            return new PlayerEffectSummary
            {
                TotalClickPowerBonus = totalClickPower,
                TotalPassiveIncomeBonus = totalPassiveIncome,
                TotalMultiplier = totalMultiplier,
                CategoryEffects = categoryEffects,
                UpgradeContributions = upgradeContributions,
                TotalUpgradeLevel = totalUpgradeLevel,
                CalculatedAt = DateTime.UtcNow
            };
        }

        public bool CanPlayerAffordUpgrade(BigNumber playerScore, BigNumber upgradeCost)
        {
            return playerScore >= upgradeCost;
        }

        public bool CanPlayerPurchaseUpgrade(Upgrade upgrade, PlayerUpgradeContext context)
        {
            // Check if upgrade is active and not hidden
            if (!upgrade.IsActive || upgrade.IsHidden) return false;

            // Check max level
            var currentLevel = context.OwnedUpgrades.GetValueOrDefault(upgrade.UpgradeId, 0);
            if (currentLevel >= upgrade.MaxLevel) return false;

            // Check prerequisites
            foreach (var prerequisite in upgrade.Prerequisites)
            {
                if (!prerequisite.IsSatisfied(context.PlayerLevel, context.CurrentScore,
                    context.ClickCount, context.OwnedUpgrades))
                {
                    return false;
                }
            }

            // Check affordability
            var cost = CalculateUpgradeCost(upgrade, currentLevel, 1);
            return CanPlayerAffordUpgrade(context.CurrentScore, cost);
        }

        public UpgradeRecommendation GetBestUpgradeForBudget(IEnumerable<Upgrade> availableUpgrades,
            PlayerUpgradeContext context, BigNumber budget)
        {
            UpgradeRecommendation? bestRecommendation = null;
            var bestEfficiency = 0m;

            foreach (var upgrade in availableUpgrades)
            {
                if (!CanPlayerPurchaseUpgrade(upgrade, context)) continue;

                var currentLevel = context.OwnedUpgrades.GetValueOrDefault(upgrade.UpgradeId, 0);
                var maxAffordableLevels = CalculateMaxAffordableLevels(upgrade, currentLevel, budget);

                if (maxAffordableLevels <= 0) continue;

                var efficiency = CalculateUpgradeEfficiency(upgrade, currentLevel, context);
                var totalCost = CalculateUpgradeCost(upgrade, currentLevel, maxAffordableLevels);
                var effectIncrease = CalculateTotalEffectIncrease(upgrade, currentLevel, maxAffordableLevels);

                if (efficiency > bestEfficiency)
                {
                    bestEfficiency = efficiency;
                    bestRecommendation = new UpgradeRecommendation
                    {
                        UpgradeId = upgrade.UpgradeId,
                        UpgradeName = upgrade.Name,
                        RecommendedLevels = maxAffordableLevels,
                        TotalCost = totalCost,
                        EfficiencyScore = efficiency,
                        ExpectedEffectIncrease = effectIncrease,
                        Reasoning = $"Best efficiency at {efficiency:F2} effect per cost unit"
                    };
                }
            }

            return bestRecommendation ?? new UpgradeRecommendation
            {
                Reasoning = "No affordable upgrades found within budget"
            };
        }

        public BulkUpgradeResult CalculateBulkUpgrade(Upgrade upgrade, PlayerUpgradeContext context,
            BigNumber maxSpendingAmount)
        {
            var currentLevel = context.OwnedUpgrades.GetValueOrDefault(upgrade.UpgradeId, 0);
            var maxAffordableLevels = CalculateMaxAffordableLevels(upgrade, currentLevel, maxSpendingAmount);
            var totalCost = CalculateUpgradeCost(upgrade, currentLevel, maxAffordableLevels);
            var remainingBudget = maxSpendingAmount - totalCost;
            var effectIncrease = CalculateTotalEffectIncrease(upgrade, currentLevel, maxAffordableLevels);

            return new BulkUpgradeResult
            {
                UpgradeId = upgrade.UpgradeId,
                LevelsCanAfford = maxAffordableLevels,
                TotalCost = totalCost,
                RemainingBudget = remainingBudget,
                EffectIncrease = effectIncrease,
                IsOptimal = maxAffordableLevels > 0
            };
        }

        public decimal CalculateUpgradeEfficiency(Upgrade upgrade, int currentLevel, PlayerUpgradeContext context)
        {
            if (currentLevel >= upgrade.MaxLevel) return 0m;

            var cost = CalculateUpgradeCost(upgrade, currentLevel, 1);
            var effectIncrease = CalculateTotalEffectIncrease(upgrade, currentLevel, 1);

            if (cost <= BigNumber.Zero) return 0m;

            // Calculate efficiency as effect increase per cost unit
            var efficiency = effectIncrease.ToDecimal() / cost.ToDecimal();

            // Apply category weights (click power might be more valuable early game)
            var categoryWeight = GetCategoryWeight(upgrade.Category, context);

            return efficiency * categoryWeight;
        }

        public UpgradePreview PreviewUpgradeEffects(Upgrade upgrade, PlayerUpgradeContext context, int levelsToAdd)
        {
            var currentLevel = context.OwnedUpgrades.GetValueOrDefault(upgrade.UpgradeId, 0);
            var newLevel = Math.Min(currentLevel + levelsToAdd, upgrade.MaxLevel);
            var actualLevelsToAdd = newLevel - currentLevel;

            var totalCost = CalculateUpgradeCost(upgrade, currentLevel, actualLevelsToAdd);
            var currentEffect = CalculateTotalEffectIncrease(upgrade, 0, currentLevel);
            var newEffect = CalculateTotalEffectIncrease(upgrade, 0, newLevel);
            var effectIncrease = newEffect - currentEffect;

            var canAfford = CanPlayerAffordUpgrade(context.CurrentScore, totalCost);
            var warnings = new List<string>();

            if (!canAfford)
            {
                warnings.Add($"Insufficient funds. Need {totalCost}, have {context.CurrentScore}");
            }

            if (actualLevelsToAdd < levelsToAdd)
            {
                warnings.Add($"Can only afford {actualLevelsToAdd} levels instead of {levelsToAdd}");
            }

            // Calculate new player effects if purchased
            var simulatedUpgrades = new Dictionary<string, int>(context.OwnedUpgrades)
            {
                [upgrade.UpgradeId] = newLevel
            };

            var newPlayerEffects = CalculateSimulatedPlayerEffects(simulatedUpgrades, upgrade);

            return new UpgradePreview
            {
                UpgradeId = upgrade.UpgradeId,
                CurrentLevel = currentLevel,
                NewLevel = newLevel,
                TotalCost = totalCost,
                CurrentEffect = currentEffect,
                NewEffect = newEffect,
                EffectIncrease = effectIncrease,
                NewPlayerEffects = newPlayerEffects,
                CanAfford = canAfford,
                PrerequisiteWarnings = warnings
            };
        }

        private int CalculateMaxAffordableLevels(Upgrade upgrade, int currentLevel, BigNumber budget)
        {
            var maxLevels = upgrade.MaxLevel - currentLevel;
            var affordableLevels = 0;
            var totalCost = BigNumber.Zero;

            for (int i = 1; i <= maxLevels; i++)
            {
                var levelCost = upgrade.Cost.CalculateCostAtLevel(currentLevel + i - 1);
                var newTotalCost = totalCost + levelCost;

                if (newTotalCost <= budget)
                {
                    totalCost = newTotalCost;
                    affordableLevels = i;
                }
                else
                {
                    break;
                }
            }

            return affordableLevels;
        }

        private BigNumber CalculateTotalEffectIncrease(Upgrade upgrade, int fromLevel, int levelsToAdd)
        {
            var totalIncrease = BigNumber.Zero;

            foreach (var effect in upgrade.Effects)
            {
                var currentEffect = CalculateUpgradeEffect(effect, fromLevel);
                var newEffect = CalculateUpgradeEffect(effect, fromLevel + levelsToAdd);
                totalIncrease = totalIncrease + (newEffect - currentEffect);
            }

            return totalIncrease;
        }

        private decimal GetCategoryWeight(UpgradeCategory category, PlayerUpgradeContext context)
        {
            // Dynamic weighting based on player progress
            return category switch
            {
                UpgradeCategory.ClickPower => context.TotalUpgradeLevel < 50 ? 1.5m : 1.0m,
                UpgradeCategory.PassiveIncome => context.TotalUpgradeLevel > 25 ? 1.3m : 0.8m,
                UpgradeCategory.Multipliers => context.TotalUpgradeLevel > 100 ? 2.0m : 1.2m,
                UpgradeCategory.Automation => 1.1m,
                UpgradeCategory.Special => 1.4m,
                UpgradeCategory.Prestige => context.TotalUpgradeLevel > 500 ? 3.0m : 0.5m,
                _ => 1.0m
            };
        }

        private PlayerEffectSummary CalculateSimulatedPlayerEffects(Dictionary<string, int> simulatedUpgrades,
            Upgrade changedUpgrade)
        {
            // This would normally require access to all upgrades, but for preview we can approximate
            // In a full implementation, this would need the repository to get all upgrade definitions
            return new PlayerEffectSummary
            {
                CalculatedAt = DateTime.UtcNow
            };
        }
    }
}