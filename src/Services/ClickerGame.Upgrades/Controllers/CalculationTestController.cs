using ClickerGame.Upgrades.Application.Services;
using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Infrastructure.Data;
using ClickerGame.Upgrades.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClickerGame.Upgrades.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CalculationTestController : ControllerBase
    {
        private readonly IUpgradeCalculationEngine _calculationEngine;
        private readonly UpgradesDbContext _context;

        public CalculationTestController(
            IUpgradeCalculationEngine calculationEngine,
            UpgradesDbContext context)
        {
            _calculationEngine = calculationEngine;
            _context = context;
        }

        [HttpGet("cost/{upgradeId}/level/{level}")]
        public async Task<ActionResult> CalculateUpgradeCost(string upgradeId, int level, int levels = 1)
        {
            var upgrade = await _context.Upgrades.FirstOrDefaultAsync(u => u.UpgradeId == upgradeId);
            if (upgrade == null) return NotFound("Upgrade not found");

            var cost = _calculationEngine.CalculateUpgradeCost(upgrade, level, levels);

            return Ok(new
            {
                upgradeId,
                currentLevel = level,
                levelsToUpgrade = levels,
                totalCost = cost.ToString(),
                costPerLevel = levels > 0 ? (cost.ToDecimal() / levels).ToString("N2") : "0"
            });
        }

        [HttpPost("preview")]
        public async Task<ActionResult> PreviewUpgrade([FromBody] UpgradePreviewRequest request)
        {
            var upgrade = await _context.Upgrades.FirstOrDefaultAsync(u => u.UpgradeId == request.UpgradeId);
            if (upgrade == null) return NotFound("Upgrade not found");

            var context = new PlayerUpgradeContext
            {
                PlayerId = request.PlayerId,
                CurrentScore = new BigNumber(request.CurrentScore),
                PlayerLevel = request.PlayerLevel,
                ClickCount = new BigNumber(request.ClickCount),
                OwnedUpgrades = request.OwnedUpgrades
            };

            var preview = _calculationEngine.PreviewUpgradeEffects(upgrade, context, request.LevelsToAdd);

            return Ok(preview);
        }

        [HttpPost("recommendation")]
        public async Task<ActionResult> GetUpgradeRecommendation([FromBody] RecommendationRequest request)
        {
            var availableUpgrades = await _context.Upgrades
                .Where(u => u.IsActive && !u.IsHidden)
                .ToListAsync();

            var context = new PlayerUpgradeContext
            {
                PlayerId = request.PlayerId,
                CurrentScore = new BigNumber(request.CurrentScore),
                PlayerLevel = request.PlayerLevel,
                ClickCount = new BigNumber(request.ClickCount),
                OwnedUpgrades = request.OwnedUpgrades
            };

            var recommendation = _calculationEngine.GetBestUpgradeForBudget(
                availableUpgrades, context, new BigNumber(request.Budget));

            return Ok(recommendation);
        }

        [HttpPost("bulk-calculate")]
        public async Task<ActionResult> CalculateBulkUpgrade([FromBody] BulkUpgradeRequest request)
        {
            var upgrade = await _context.Upgrades.FirstOrDefaultAsync(u => u.UpgradeId == request.UpgradeId);
            if (upgrade == null) return NotFound("Upgrade not found");

            var context = new PlayerUpgradeContext
            {
                PlayerId = request.PlayerId,
                CurrentScore = new BigNumber(request.CurrentScore),
                OwnedUpgrades = request.OwnedUpgrades
            };

            var result = _calculationEngine.CalculateBulkUpgrade(
                upgrade, context, new BigNumber(request.MaxSpending));

            return Ok(result);
        }
    }

    // DTOs for test endpoints
    public class UpgradePreviewRequest
    {
        public Guid PlayerId { get; set; }
        public string UpgradeId { get; set; } = string.Empty;
        public decimal CurrentScore { get; set; }
        public int PlayerLevel { get; set; }
        public decimal ClickCount { get; set; }
        public int LevelsToAdd { get; set; }
        public Dictionary<string, int> OwnedUpgrades { get; set; } = new();
    }

    public class RecommendationRequest
    {
        public Guid PlayerId { get; set; }
        public decimal CurrentScore { get; set; }
        public int PlayerLevel { get; set; }
        public decimal ClickCount { get; set; }
        public decimal Budget { get; set; }
        public Dictionary<string, int> OwnedUpgrades { get; set; } = new();
    }

    public class BulkUpgradeRequest
    {
        public Guid PlayerId { get; set; }
        public string UpgradeId { get; set; } = string.Empty;
        public decimal CurrentScore { get; set; }
        public decimal MaxSpending { get; set; }
        public Dictionary<string, int> OwnedUpgrades { get; set; } = new();
    }
}