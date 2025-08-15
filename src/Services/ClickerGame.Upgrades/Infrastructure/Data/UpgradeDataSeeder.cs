using ClickerGame.Upgrades.Domain.Entities;
using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClickerGame.Upgrades.Infrastructure.Data
{
    public static class UpgradeDataSeeder
    {
        public static async Task SeedUpgradesAsync(UpgradesDbContext context)
        {
            // Check if upgrades already exist
            if (await context.Upgrades.AnyAsync())
            {
                return; // Already seeded
            }

            var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var upgrades = new[]
            {
                new Upgrade
                {
                    UpgradeId = "click_power_1",
                    Name = "Stronger Fingers",
                    Description = "Increases click power by 1 per level",
                    Category = UpgradeCategory.ClickPower,
                    Rarity = UpgradeRarity.Common,
                    Cost = new UpgradeCost(new BigNumber(10), 1.15m),
                    Effects = new List<UpgradeEffect>
                    {
                        new UpgradeEffect(UpgradeCategory.ClickPower, UpgradeType.Linear, BigNumber.One, description: "+1 click power per level")
                    },
                    Prerequisites = new List<UpgradePrerequisite>(),
                    MaxLevel = 500,
                    IsActive = true,
                    IsHidden = false,
                    CreatedAt = seedDate,
                    IconUrl = null,
                    UpdatedAt = null
                },
                new Upgrade
                {
                    UpgradeId = "passive_income_1",
                    Name = "Auto-Clicker",
                    Description = "Generates passive income per second",
                    Category = UpgradeCategory.PassiveIncome,
                    Rarity = UpgradeRarity.Common,
                    Cost = new UpgradeCost(new BigNumber(100), 1.2m),
                    Effects = new List<UpgradeEffect>
                    {
                        new UpgradeEffect(UpgradeCategory.PassiveIncome, UpgradeType.Linear, new BigNumber(1), description: "+1 score per second per level")
                    },
                    Prerequisites = new List<UpgradePrerequisite>
                    {
                        new UpgradePrerequisite(PrerequisiteType.TotalScore, new BigNumber(500), description: "Requires 500 total score")
                    },
                    MaxLevel = 100,
                    IsActive = true,
                    IsHidden = false,
                    CreatedAt = seedDate,
                    IconUrl = null,
                    UpdatedAt = null
                },
                new Upgrade
                {
                    UpgradeId = "multiplier_1",
                    Name = "Efficiency Expert",
                    Description = "Multiplies all income by 2x",
                    Category = UpgradeCategory.Multipliers,
                    Rarity = UpgradeRarity.Rare,
                    Cost = new UpgradeCost(new BigNumber(1000), 2.0m),
                    Effects = new List<UpgradeEffect>
                    {
                        new UpgradeEffect(UpgradeCategory.Multipliers, UpgradeType.Exponential, new BigNumber(2), description: "2x multiplier per level")
                    },
                    Prerequisites = new List<UpgradePrerequisite>
                    {
                        new UpgradePrerequisite(PrerequisiteType.OtherUpgrade, BigNumber.Zero, 10, "click_power_1", "Requires Stronger Fingers level 10")
                    },
                    MaxLevel = 10,
                    IsActive = true,
                    IsHidden = false,
                    CreatedAt = seedDate,
                    IconUrl = null,
                    UpdatedAt = null
                },
                new Upgrade
                {
                    UpgradeId = "click_power_2",
                    Name = "Enhanced Clicking",
                    Description = "Advanced clicking technique",
                    Category = UpgradeCategory.ClickPower,
                    Rarity = UpgradeRarity.Uncommon,
                    Cost = new UpgradeCost(new BigNumber(1000), 1.25m),
                    Effects = new List<UpgradeEffect>
                    {
                        new UpgradeEffect(UpgradeCategory.ClickPower, UpgradeType.Linear, new BigNumber(5), description: "+5 click power per level")
                    },
                    Prerequisites = new List<UpgradePrerequisite>
                    {
                        new UpgradePrerequisite(PrerequisiteType.OtherUpgrade, BigNumber.Zero, 25, "click_power_1", "Requires Stronger Fingers level 25")
                    },
                    MaxLevel = 200,
                    IsActive = true,
                    IsHidden = false,
                    CreatedAt = seedDate,
                    IconUrl = null,
                    UpdatedAt = null
                },
                new Upgrade
                {
                    UpgradeId = "passive_income_2",
                    Name = "Auto-Farm",
                    Description = "Automated scoring system",
                    Category = UpgradeCategory.PassiveIncome,
                    Rarity = UpgradeRarity.Uncommon,
                    Cost = new UpgradeCost(new BigNumber(5000), 1.3m),
                    Effects = new List<UpgradeEffect>
                    {
                        new UpgradeEffect(UpgradeCategory.PassiveIncome, UpgradeType.Linear, new BigNumber(10), description: "+10 score per second per level")
                    },
                    Prerequisites = new List<UpgradePrerequisite>
                    {
                        new UpgradePrerequisite(PrerequisiteType.OtherUpgrade, BigNumber.Zero, 50, "passive_income_1", "Requires Auto-Clicker level 50")
                    },
                    MaxLevel = 75,
                    IsActive = true,
                    IsHidden = false,
                    CreatedAt = seedDate,
                    IconUrl = null,
                    UpdatedAt = null
                }
            };

            context.Upgrades.AddRange(upgrades);
            await context.SaveChangesAsync();
        }
    }
}