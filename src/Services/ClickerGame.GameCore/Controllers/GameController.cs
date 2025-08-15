using ClickerGame.GameCore.Application.Services;
using ClickerGame.GameCore.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClickerGame.GameCore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GameController : ControllerBase
    {
        private readonly IGameEngineService _gameEngine;
        private readonly ILogger<GameController> _logger;

        public GameController(IGameEngineService gameEngine, ILogger<GameController> logger)
        {
            _gameEngine = gameEngine;
            _logger = logger;
        }

        [HttpPost("click")]
        public async Task<ActionResult<ClickResponseDto>> ProcessClick([FromBody] ClickRequestDto request)
        {
            try
            {
                var playerId = GetPlayerIdFromToken();
                var clickPower = new BigNumber(request.ClickPower);

                var earnedValue = await _gameEngine.ProcessClickAsync(playerId, clickPower);
                var session = await _gameEngine.GetGameSessionAsync(playerId);

                return Ok(new ClickResponseDto
                {
                    EarnedValue = earnedValue.ToString(),
                    TotalScore = session.Score.ToString(),
                    ClickCount = session.ClickCount
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing click");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("session")]
        public async Task<ActionResult<GameSessionDto>> GetGameSession()
        {
            try
            {
                var playerId = GetPlayerIdFromToken();
                var session = await _gameEngine.GetGameSessionAsync(playerId);

                return Ok(new GameSessionDto
                {
                    SessionId = session.SessionId,
                    PlayerId = session.PlayerId,
                    PlayerUsername = session.PlayerUsername,
                    Score = session.Score.ToString(),
                    ClickCount = session.ClickCount,
                    ClickPower = session.ClickPower.ToString(),
                    PassiveIncomePerSecond = session.PassiveIncomePerSecond,
                    IsActive = session.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting game session");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("session/create")]
        public async Task<ActionResult<GameSessionDto>> CreateGameSession()
        {
            try
            {
                var playerId = GetPlayerIdFromToken();
                var username = GetUsernameFromToken();

                var session = await _gameEngine.CreateGameSessionAsync(playerId, username);

                return Ok(new GameSessionDto
                {
                    SessionId = session.SessionId,
                    PlayerId = session.PlayerId,
                    PlayerUsername = session.PlayerUsername,
                    Score = session.Score.ToString(),
                    ClickCount = session.ClickCount,
                    ClickPower = session.ClickPower.ToString(),
                    PassiveIncomePerSecond = session.PassiveIncomePerSecond,
                    IsActive = session.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating game session");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("offline-earnings")]
        public async Task<ActionResult<OfflineEarningsDto>> GetOfflineEarnings()
        {
            try
            {
                var playerId = GetPlayerIdFromToken();
                var earnings = await _gameEngine.CalculateOfflineEarningsAsync(playerId);

                return Ok(new OfflineEarningsDto
                {
                    OfflineEarnings = earnings.ToString(),
                    Message = earnings > BigNumber.Zero ?
                        $"Welcome back! You earned {earnings} while away!" :
                        "Welcome back!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating offline earnings");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("health")]
        [AllowAnonymous]
        public ActionResult GetHealth()
        {
            return Ok(new
            {
                status = "Healthy",
                service = "GameCore Service",
                timestamp = DateTime.UtcNow
            });
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

        private string GetUsernameFromToken()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        }
    }

    public class ClickRequestDto
    {
        public decimal ClickPower { get; set; } = 1;
    }

    public class ClickResponseDto
    {
        public string EarnedValue { get; set; } = string.Empty;
        public string TotalScore { get; set; } = string.Empty;
        public long ClickCount { get; set; }
    }

    public class GameSessionDto
    {
        public Guid SessionId { get; set; }
        public Guid PlayerId { get; set; }
        public string PlayerUsername { get; set; } = string.Empty;
        public string Score { get; set; } = string.Empty;
        public long ClickCount { get; set; }
        public string ClickPower { get; set; } = string.Empty;
        public decimal PassiveIncomePerSecond { get; set; }
        public bool IsActive { get; set; }
    }

    public class OfflineEarningsDto
    {
        public string OfflineEarnings { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}