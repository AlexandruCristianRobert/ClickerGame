using ClickerGame.Upgrades.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClickerGame.Upgrades.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseTestController : ControllerBase
    {
        private readonly UpgradesDbContext _context;

        public DatabaseTestController(UpgradesDbContext context)
        {
            _context = context;
        }

        [HttpGet("connection")]
        public async Task<ActionResult> TestConnection()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                await _context.Database.CloseConnectionAsync();

                var upgradeCount = await _context.Upgrades.CountAsync();
                var playerUpgradeCount = await _context.PlayerUpgrades.CountAsync();

                return Ok(new
                {
                    status = "Connected",
                    upgradeCount,
                    playerUpgradeCount,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "Failed",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpGet("upgrades")]
        public async Task<ActionResult> GetSeedUpgrades()
        {
            try
            {
                var upgrades = await _context.Upgrades
                    .Where(u => u.IsActive)
                    .Select(u => new
                    {
                        u.UpgradeId,
                        u.Name,
                        u.Description,
                        u.Category,
                        u.Rarity,
                        u.MaxLevel,
                        u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(upgrades);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }
    }
}