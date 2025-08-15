using ClickerGame.Upgrades.Application.Services;
using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClickerGame.Upgrades.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PlayerUpgradesController : ControllerBase
    {
        private readonly IUpgradeService _upgradeService;
        private readonly ILogger<PlayerUpgradesController> _logger;
        private readonly ICorrelationService _correlationService;

        public PlayerUpgradesController(
            IUpgradeService upgradeService,
            ILogger<PlayerUpgradesController> logger,
            ICorrelationService correlationService)
        {
            _upgradeService = upgradeService;
            _logger = logger;
            _correlationService = correlationService;
        }

        /// <summary>
        /// Get all upgrades owned by the authenticated player
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PlayerUpgradeDto>>> GetPlayerUpgrades()
        {
            _logger.LogRequestStart(_correlationService, "GetPlayerUpgrades");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var playerUpgrades = await _upgradeService.GetPlayerUpgradesAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "PlayerUpgradesRetrieved",
                    new { PlayerId = playerId, Count = playerUpgrades.Count() });

                return Ok(playerUpgrades);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting player upgrades");
                return StatusCode(500, new { error = "An error occurred while retrieving player upgrades" });
            }
        }

        /// <summary>
        /// Get player's upgrade progress and statistics
        /// </summary>
        [HttpGet("progress")]
        public async Task<ActionResult<PlayerUpgradeProgressDto>> GetPlayerProgress()
        {
            _logger.LogRequestStart(_correlationService, "GetPlayerProgress");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var progress = await _upgradeService.GetPlayerUpgradeProgressAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "PlayerProgressRetrieved",
                    new { PlayerId = playerId, TotalUpgrades = progress.TotalUpgradesOwned });

                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting player progress");
                return StatusCode(500, new { error = "An error occurred while retrieving player progress" });
            }
        }

        /// <summary>
        /// Get player's current effects from all upgrades
        /// </summary>
        [HttpGet("effects")]
        public async Task<ActionResult<PlayerEffectSummary>> GetPlayerEffects()
        {
            _logger.LogRequestStart(_correlationService, "GetPlayerEffects");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var effects = await _upgradeService.CalculatePlayerEffectsAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "PlayerEffectsRetrieved",
                    new { PlayerId = playerId, TotalClickPower = effects.TotalClickPowerBonus.ToString() });

                return Ok(effects);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting player effects");
                return StatusCode(500, new { error = "An error occurred while calculating player effects" });
            }
        }

        /// <summary>
        /// Get efficiency analysis for player's upgrade choices
        /// </summary>
        [HttpGet("efficiency-analysis")]
        public async Task<ActionResult<UpgradeEfficiencyAnalysis>> GetEfficiencyAnalysis()
        {
            _logger.LogRequestStart(_correlationService, "GetEfficiencyAnalysis");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var analysis = await _upgradeService.AnalyzeUpgradeEfficiencyAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "EfficiencyAnalysisGenerated",
                    new { PlayerId = playerId });

                return Ok(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error generating efficiency analysis");
                return StatusCode(500, new { error = "An error occurred while generating efficiency analysis" });
            }
        }

        /// <summary>
        /// Reset all player upgrades (admin or testing feature)
        /// </summary>
        [HttpDelete("reset")]
        public async Task<ActionResult> ResetPlayerUpgrades()
        {
            _logger.LogRequestStart(_correlationService, "ResetPlayerUpgrades");

            try
            {
                var playerId = GetPlayerIdFromToken();
                await _upgradeService.ResetPlayerUpgradesAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "PlayerUpgradesReset",
                    new { PlayerId = playerId });

                return Ok(new { message = "Player upgrades reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error resetting player upgrades");
                return StatusCode(500, new { error = "An error occurred while resetting upgrades" });
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