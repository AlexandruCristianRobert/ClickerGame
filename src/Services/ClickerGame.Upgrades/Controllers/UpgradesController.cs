using ClickerGame.Upgrades.Application.Services;
using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.Enums;
using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClickerGame.Upgrades.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UpgradesController : ControllerBase
    {
        private readonly IUpgradeService _upgradeService;
        private readonly ILogger<UpgradesController> _logger;
        private readonly ICorrelationService _correlationService;

        public UpgradesController(
            IUpgradeService upgradeService,
            ILogger<UpgradesController> logger,
            ICorrelationService correlationService)
        {
            _upgradeService = upgradeService;
            _logger = logger;
            _correlationService = correlationService;
        }

        /// <summary>
        /// Get all available upgrades for the authenticated player
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UpgradeDto>>> GetAvailableUpgrades()
        {
            _logger.LogRequestStart(_correlationService, "GetAvailableUpgrades");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var upgrades = await _upgradeService.GetAvailableUpgradesAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "AvailableUpgradesRetrieved",
                    new { PlayerId = playerId, Count = upgrades.Count() });

                return Ok(upgrades);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogBusinessEvent(_correlationService, "UnauthorizedUpgradeAccess", new { Reason = ex.Message });
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting available upgrades");
                return StatusCode(500, new { error = "An error occurred while retrieving upgrades" });
            }
        }

        /// <summary>
        /// Get upgrades by category for the authenticated player
        /// </summary>
        [HttpGet("category/{category}")]
        public async Task<ActionResult<IEnumerable<UpgradeDto>>> GetUpgradesByCategory(UpgradeCategory category)
        {
            _logger.LogRequestStart(_correlationService, "GetUpgradesByCategory");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var upgrades = await _upgradeService.GetUpgradesByCategoryAsync(category, playerId);

                _logger.LogBusinessEvent(_correlationService, "UpgradesByCategoryRetrieved",
                    new { PlayerId = playerId, Category = category.ToString(), Count = upgrades.Count() });

                return Ok(upgrades);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting upgrades by category {Category}", category);
                return StatusCode(500, new { error = "An error occurred while retrieving upgrades" });
            }
        }

        /// <summary>
        /// Get a specific upgrade by ID
        /// </summary>
        [HttpGet("{upgradeId}")]
        public async Task<ActionResult<UpgradeDto>> GetUpgrade(string upgradeId)
        {
            _logger.LogRequestStart(_correlationService, "GetUpgrade");

            try
            {
                var upgrade = await _upgradeService.GetUpgradeAsync(upgradeId);

                _logger.LogBusinessEvent(_correlationService, "UpgradeRetrieved",
                    new { UpgradeId = upgradeId });

                return Ok(upgrade);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogBusinessEvent(_correlationService, "UpgradeNotFound", new { UpgradeId = upgradeId });
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting upgrade {UpgradeId}", upgradeId);
                return StatusCode(500, new { error = "An error occurred while retrieving the upgrade" });
            }
        }

        /// <summary>
        /// Purchase an upgrade
        /// </summary>
        [HttpPost("purchase")]
        public async Task<ActionResult<UpgradePurchaseResult>> PurchaseUpgrade([FromBody] PurchaseUpgradeRequest request)
        {
            _logger.LogRequestStart(_correlationService, "PurchaseUpgrade");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var result = await _upgradeService.PurchaseUpgradeAsync(playerId, request);

                if (result.Success)
                {
                    _logger.LogBusinessEvent(_correlationService, "UpgradePurchaseSuccess",
                        new { PlayerId = playerId, UpgradeId = request.UpgradeId, LevelsPurchased = result.LevelsPurchased });
                    return Ok(result);
                }
                else
                {
                    _logger.LogBusinessEvent(_correlationService, "UpgradePurchaseFailed",
                        new { PlayerId = playerId, UpgradeId = request.UpgradeId, Errors = result.Errors });
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error purchasing upgrade {UpgradeId}", request.UpgradeId);
                return StatusCode(500, new { error = "An error occurred during the upgrade purchase" });
            }
        }

        /// <summary>
        /// Purchase multiple upgrades in bulk
        /// </summary>
        [HttpPost("purchase/bulk")]
        public async Task<ActionResult<BulkUpgradePurchaseResult>> PurchaseUpgradesBulk([FromBody] BulkPurchaseRequest request)
        {
            _logger.LogRequestStart(_correlationService, "PurchaseUpgradesBulk");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var result = await _upgradeService.PurchaseUpgradeBulkAsync(playerId, request);

                _logger.LogBusinessEvent(_correlationService, "BulkUpgradePurchaseCompleted",
                    new { PlayerId = playerId, Successful = result.SuccessfulPurchases, Failed = result.FailedPurchases });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error during bulk upgrade purchase");
                return StatusCode(500, new { error = "An error occurred during the bulk upgrade purchase" });
            }
        }

        /// <summary>
        /// Preview upgrade effects before purchasing
        /// </summary>
        [HttpPost("preview")]
        public async Task<ActionResult<UpgradePreview>> PreviewUpgrade([FromBody] PreviewUpgradeRequest request)
        {
            _logger.LogRequestStart(_correlationService, "PreviewUpgrade");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var preview = await _upgradeService.PreviewUpgradePurchaseAsync(playerId, request);

                _logger.LogBusinessEvent(_correlationService, "UpgradePreviewGenerated",
                    new { PlayerId = playerId, UpgradeId = request.UpgradeId, LevelsToAdd = request.LevelsToPurchase });

                return Ok(preview);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error previewing upgrade {UpgradeId}", request.UpgradeId);
                return StatusCode(500, new { error = "An error occurred while generating the preview" });
            }
        }

        /// <summary>
        /// Get upgrade recommendations for the player
        /// </summary>
        [HttpGet("recommendations")]
        public async Task<ActionResult<IEnumerable<UpgradeRecommendation>>> GetUpgradeRecommendations(
            [FromQuery] decimal budget = 0,
            [FromQuery] int count = 5)
        {
            _logger.LogRequestStart(_correlationService, "GetUpgradeRecommendations");

            try
            {
                var playerId = GetPlayerIdFromToken();

                IEnumerable<UpgradeRecommendation> recommendations;
                if (budget > 0)
                {
                    var singleRecommendation = await _upgradeService.GetUpgradeRecommendationAsync(playerId, new BigNumber(budget));
                    recommendations = new[] { singleRecommendation };
                }
                else
                {
                    recommendations = await _upgradeService.GetTopUpgradeRecommendationsAsync(playerId, count);
                }

                _logger.LogBusinessEvent(_correlationService, "UpgradeRecommendationsGenerated",
                    new { PlayerId = playerId, Budget = budget, Count = recommendations.Count() });

                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting upgrade recommendations");
                return StatusCode(500, new { error = "An error occurred while generating recommendations" });
            }
        }

        /// <summary>
        /// Get upgrade statistics for analytics
        /// </summary>
        [HttpGet("{upgradeId}/statistics")]
        public async Task<ActionResult<UpgradeStatistics>> GetUpgradeStatistics(string upgradeId)
        {
            _logger.LogRequestStart(_correlationService, "GetUpgradeStatistics");

            try
            {
                var statistics = await _upgradeService.GetUpgradeStatisticsAsync(upgradeId);

                _logger.LogBusinessEvent(_correlationService, "UpgradeStatisticsRetrieved",
                    new { UpgradeId = upgradeId });

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting upgrade statistics for {UpgradeId}", upgradeId);
                return StatusCode(500, new { error = "An error occurred while retrieving statistics" });
            }
        }

        private Guid GetPlayerIdFromToken()
        {
            var playerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(playerIdClaim, out var playerId))
            {
                return playerId;
            }
            throw new UnauthorizedAccessException("Invalid player ID in token");
        }
    }
}