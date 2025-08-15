using ClickerGame.Upgrades.Domain.Entities;
using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Application.DTOs;

namespace ClickerGame.Upgrades.Application.Services
{
    public interface IUpgradeCalculationEngine
    {
        // Cost Calculations
        BigNumber CalculateUpgradeCost(Upgrade upgrade, int currentLevel, int levelsToUpgrade = 1);
        BigNumber CalculateTotalCostForLevels(Upgrade upgrade, int fromLevel, int toLevel);

        // Effect Calculations
        BigNumber CalculateUpgradeEffect(UpgradeEffect effect, int level);
        PlayerEffectSummary CalculatePlayerEffects(IEnumerable<PlayerUpgrade> playerUpgrades);

        // Purchase Validation
        bool CanPlayerAffordUpgrade(BigNumber playerScore, BigNumber upgradeCost);
        bool CanPlayerPurchaseUpgrade(Upgrade upgrade, PlayerUpgradeContext context);

        // Optimization Calculations
        UpgradeRecommendation GetBestUpgradeForBudget(IEnumerable<Upgrade> availableUpgrades,
            PlayerUpgradeContext context, BigNumber budget);

        // Bulk Operations
        BulkUpgradeResult CalculateBulkUpgrade(Upgrade upgrade, PlayerUpgradeContext context,
            BigNumber maxSpendingAmount);

        // Efficiency Calculations
        decimal CalculateUpgradeEfficiency(Upgrade upgrade, int currentLevel, PlayerUpgradeContext context);

        // Preview Calculations
        UpgradePreview PreviewUpgradeEffects(Upgrade upgrade, PlayerUpgradeContext context, int levelsToAdd);
    }
}