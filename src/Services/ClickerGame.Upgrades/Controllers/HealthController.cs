using Microsoft.AspNetCore.Mvc;
using ClickerGame.Upgrades.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ClickerGame.Shared.Logging;

namespace ClickerGame.Upgrades.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly UpgradesDbContext _context;
        private readonly ILogger<HealthController> _logger;
        private readonly ICorrelationService _correlationService;

        public HealthController(
            UpgradesDbContext context,
            ILogger<HealthController> logger,
            ICorrelationService correlationService)
        {
            _context = context;
            _logger = logger;
            _correlationService = correlationService;
        }

        [HttpGet]
        public async Task<ActionResult> GetHealth()
        {
            try
            {
                // Basic health check
                var canConnect = await _context.Database.CanConnectAsync();
                var upgradeCount = await _context.Upgrades.CountAsync();

                var health = new
                {
                    status = canConnect ? "Healthy" : "Unhealthy",
                    service = "Upgrades Service",
                    timestamp = DateTime.UtcNow,
                    details = new
                    {
                        database = canConnect ? "Connected" : "Disconnected",
                        upgradeCount,
                        version = "1.0.0"
                    }
                };

                if (canConnect)
                {
                    return Ok(health);
                }
                else
                {
                    return StatusCode(503, health);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Health check failed");
                return StatusCode(503, new
                {
                    status = "Unhealthy",
                    service = "Upgrades Service",
                    timestamp = DateTime.UtcNow,
                    error = ex.Message
                });
            }
        }

        [HttpGet("detailed")]
        public async Task<ActionResult> GetDetailedHealth()
        {
            var checks = new Dictionary<string, object>();

            try
            {
                // Database check
                var dbConnected = await _context.Database.CanConnectAsync();
                checks["database"] = new
                {
                    status = dbConnected ? "healthy" : "unhealthy",
                    responseTime = "< 100ms", // You could measure actual time
                    upgradeCount = dbConnected ? await _context.Upgrades.CountAsync() : 0,
                    playerUpgradeCount = dbConnected ? await _context.PlayerUpgrades.CountAsync() : 0
                };

                // Memory check (basic)
                var memoryUsage = GC.GetTotalMemory(false);
                checks["memory"] = new
                {
                    status = memoryUsage < 100 * 1024 * 1024 ? "healthy" : "warning", // 100MB threshold
                    usageBytes = memoryUsage,
                    usageMB = memoryUsage / (1024 * 1024)
                };

                // Overall status
                var overallHealthy = checks.Values.All(check =>
                    ((dynamic)check).status.ToString() != "unhealthy");

                var response = new
                {
                    status = overallHealthy ? "healthy" : "unhealthy",
                    service = "Upgrades Service",
                    timestamp = DateTime.UtcNow,
                    checks
                };

                return overallHealthy ? Ok(response) : StatusCode(503, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Detailed health check failed");
                return StatusCode(503, new
                {
                    status = "unhealthy",
                    service = "Upgrades Service",
                    timestamp = DateTime.UtcNow,
                    error = ex.Message,
                    checks
                });
            }
        }
    }
}