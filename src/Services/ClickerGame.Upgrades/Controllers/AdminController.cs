using ClickerGame.Upgrades.Application.Services;
using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.Enums;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickerGame.Upgrades.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IUpgradeService _upgradeService;
        private readonly ILogger<AdminController> _logger;
        private readonly ICorrelationService _correlationService;

        public AdminController(
            IUpgradeService upgradeService,
            ILogger<AdminController> logger,
            ICorrelationService correlationService)
        {
            _upgradeService = upgradeService;
            _logger = logger;
            _correlationService = correlationService;
        }

        /// <summary>
        /// Unlock a specific upgrade for a player
        /// </summary>
        [HttpPost("players/{playerId:guid}/upgrades/{upgradeId}/unlock")]
        public async Task<ActionResult> UnlockUpgradeForPlayer(Guid playerId, string upgradeId)
        {
            _logger.LogRequestStart(_correlationService, "UnlockUpgradeForPlayer");

            try
            {
                var success = await _upgradeService.UnlockUpgradeForPlayerAsync(playerId, upgradeId);

                if (success)
                {
                    _logger.LogBusinessEvent(_correlationService, "UpgradeUnlockedForPlayer",
                        new { PlayerId = playerId, UpgradeId = upgradeId });
                    return Ok(new { message = "Upgrade unlocked successfully" });
                }
                else
                {
                    return BadRequest(new { error = "Failed to unlock upgrade" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error unlocking upgrade {UpgradeId} for player {PlayerId}",
                    upgradeId, playerId);
                return StatusCode(500, new { error = "An error occurred while unlocking the upgrade" });
            }
        }

        /// <summary>
        /// Reset upgrades for a specific player
        /// </summary>
        [HttpDelete("players/{playerId:guid}/upgrades")]
        public async Task<ActionResult> ResetPlayerUpgrades(Guid playerId)
        {
            _logger.LogRequestStart(_correlationService, "AdminResetPlayerUpgrades");

            try
            {
                await _upgradeService.ResetPlayerUpgradesAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "AdminPlayerUpgradesReset",
                    new { PlayerId = playerId });

                return Ok(new { message = "Player upgrades reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error resetting upgrades for player {PlayerId}", playerId);
                return StatusCode(500, new { error = "An error occurred while resetting upgrades" });
            }
        }

        /// <summary>
        /// Get comprehensive statistics for all upgrades
        /// </summary>
        [HttpGet("upgrades/statistics")]
        public async Task<ActionResult<Dictionary<string, UpgradeStatistics>>> GetAllUpgradeStatistics()
        {
            _logger.LogRequestStart(_correlationService, "GetAllUpgradeStatistics");

            try
            {
                // This would need to be implemented in the service
                var statistics = new Dictionary<string, UpgradeStatistics>();

                // For demo purposes, get statistics for known upgrades
                var knownUpgrades = new[] { "click_power_1", "passive_income_1", "multiplier_1" };
                foreach (var upgradeId in knownUpgrades)
                {
                    try
                    {
                        statistics[upgradeId] = await _upgradeService.GetUpgradeStatisticsAsync(upgradeId);
                    }
                    catch
                    {
                        // Skip upgrades that don't exist
                    }
                }

                _logger.LogBusinessEvent(_correlationService, "AllUpgradeStatisticsRetrieved",
                    new { Count = statistics.Count });

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting all upgrade statistics");
                return StatusCode(500, new { error = "An error occurred while retrieving statistics" });
            }
        }

        /// <summary>
        /// Get player progress by player ID (admin view)
        /// </summary>
        [HttpGet("players/{playerId:guid}/progress")]
        public async Task<ActionResult<PlayerUpgradeProgressDto>> GetPlayerProgressById(Guid playerId)
        {
            _logger.LogRequestStart(_correlationService, "GetPlayerProgressById");

            try
            {
                var progress = await _upgradeService.GetPlayerUpgradeProgressAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "AdminPlayerProgressRetrieved",
                    new { PlayerId = playerId, TotalUpgrades = progress.TotalUpgradesOwned });

                return Ok(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting progress for player {PlayerId}", playerId);
                return StatusCode(500, new { error = "An error occurred while retrieving player progress" });
            }
        }
    }
}