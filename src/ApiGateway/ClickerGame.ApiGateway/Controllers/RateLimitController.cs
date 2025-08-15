using ClickerGame.ApiGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClickerGame.ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RateLimitController : ControllerBase
    {
        private readonly IRateLimitService _rateLimitService;
        private readonly ILogger<RateLimitController> _logger;

        public RateLimitController(IRateLimitService rateLimitService, ILogger<RateLimitController> logger)
        {
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        [HttpGet("status")]
        [Authorize]
        public async Task<ActionResult<RateLimitStatusDto>> GetRateLimitStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID not found in token");
            }

            var clientId = $"user:{userId}";
            var info = await _rateLimitService.GetRateLimitInfoAsync(clientId, "get:/api/game/click");

            return Ok(new RateLimitStatusDto
            {
                ClientId = clientId,
                CurrentCount = info.CurrentCount,
                Limit = info.Limit,
                ResetTime = info.ResetTime,
                RemainingRequests = Math.Max(0, info.Limit - info.CurrentCount)
            });
        }

        [HttpPost("reset")]
        [Authorize]
        public async Task<ActionResult> ResetUserRateLimit()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID not found in token");
            }

            var clientId = $"user:{userId}";
            await _rateLimitService.ResetClientLimitsAsync(clientId);

            _logger.LogInformation("Rate limits reset for user {UserId}", userId);
            return Ok(new { message = "Rate limits reset successfully" });
        }

        [HttpGet("info")]
        public ActionResult<RateLimitConfigDto> GetRateLimitInfo()
        {
            return Ok(new RateLimitConfigDto
            {
                Endpoints = new Dictionary<string, EndpointRateLimit>
                {
                    {
                        "/api/players/register",
                        new EndpointRateLimit { Limit = 5, Period = "1h", Description = "User registration" }
                    },
                    {
                        "/api/players/login",
                        new EndpointRateLimit { Limit = 10, Period = "15m", Description = "User login" }
                    },
                    {
                        "/api/game/click",
                        new EndpointRateLimit { Limit = 1000, Period = "1m", Description = "Game clicks" }
                    },
                    {
                        "/api/game/session",
                        new EndpointRateLimit { Limit = 60, Period = "1m", Description = "Session requests" }
                    }
                }
            });
        }
    }

    public class RateLimitStatusDto
    {
        public string ClientId { get; set; } = string.Empty;
        public long CurrentCount { get; set; }
        public long Limit { get; set; }
        public DateTime ResetTime { get; set; }
        public long RemainingRequests { get; set; }
    }

    public class RateLimitConfigDto
    {
        public Dictionary<string, EndpointRateLimit> Endpoints { get; set; } = new();
    }

    public class EndpointRateLimit
    {
        public long Limit { get; set; }
        public string Period { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}