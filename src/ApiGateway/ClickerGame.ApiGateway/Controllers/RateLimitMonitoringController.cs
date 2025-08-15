using ClickerGame.ApiGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickerGame.ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RateLimitMonitoringController : ControllerBase
    {
        private readonly IRateLimitMonitoringService _monitoringService;

        public RateLimitMonitoringController(IRateLimitMonitoringService monitoringService)
        {
            _monitoringService = monitoringService;
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<RateLimitStatistics>> GetStatistics()
        {
            var stats = await _monitoringService.GetStatisticsAsync();
            return Ok(stats);
        }

        [HttpGet("violations")]
        public async Task<ActionResult<List<RateLimitViolation>>> GetRecentViolations([FromQuery] int count = 50)
        {
            var violations = await _monitoringService.GetRecentViolationsAsync(count);
            return Ok(violations);
        }
    }
}