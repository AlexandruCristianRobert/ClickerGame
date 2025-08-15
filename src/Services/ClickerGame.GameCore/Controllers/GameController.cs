using ClickerGame.GameCore.Application.Services;
using ClickerGame.GameCore.Domain.ValueObjects;
using ClickerGame.Shared.Logging;
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
        private readonly ICorrelationService _correlationService;

        public GameController(
            IGameEngineService gameEngine,
            ILogger<GameController> logger,
            ICorrelationService correlationService)
        {
            _gameEngine = gameEngine;
            _logger = logger;
            _correlationService = correlationService;
        }

        [HttpPost("click")]
        public async Task<ActionResult<ClickResponseDto>> ProcessClick([FromBody] ClickRequestDto request)
        {
            _logger.LogRequestStart(_correlationService, "ProcessClick");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var clickPower = new BigNumber(request.ClickPower);

                _logger.LogBusinessEvent(_correlationService, "ClickProcessing", new { PlayerId = playerId, ClickPower = request.ClickPower });

                var earnedValue = await _gameEngine.ProcessClickAsync(playerId, clickPower);
                var session = await _gameEngine.GetGameSessionAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "ClickProcessed", new
                {
                    PlayerId = playerId,
                    EarnedValue = earnedValue.ToString(),
                    TotalScore = session.Score.ToString(),
                    ClickCount = session.ClickCount
                });

                return Ok(new ClickResponseDto
                {
                    EarnedValue = earnedValue.ToString(),
                    TotalScore = session.Score.ToString(),
                    ClickCount = session.ClickCount
                });
            }
            catch (InvalidOperationException ex)
            {
                var playerId = GetPlayerIdFromToken();
                _logger.LogBusinessEvent(_correlationService, "ClickRateLimited", new { PlayerId = playerId, Reason = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error processing click");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("session")]
        public async Task<ActionResult<GameSessionDto>> GetGameSession()
        {
            _logger.LogRequestStart(_correlationService, "GetGameSession");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var session = await _gameEngine.GetGameSessionAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "GameSessionRetrieved", new
                {
                    PlayerId = playerId,
                    SessionId = session.SessionId,
                    Score = session.Score.ToString(),
                    ClickCount = session.ClickCount
                });

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
                _logger.LogError(_correlationService, ex, "Error getting game session");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get game session by player ID (for service-to-service calls)
        /// </summary>
        [HttpGet("session/{playerId:guid}")]
        public async Task<ActionResult<GameSessionDto>> GetGameSessionByPlayerId(Guid playerId)
        {
            _logger.LogRequestStart(_correlationService, "GetGameSessionByPlayerId");

            try
            {
                var session = await _gameEngine.GetGameSessionAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "GameSessionRetrievedByPlayerId", new
                {
                    PlayerId = playerId,
                    SessionId = session.SessionId,
                    Score = session.Score.ToString(),
                    ClickCount = session.ClickCount
                });

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
                _logger.LogError(_correlationService, ex, "Error getting game session for player {PlayerId}", playerId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Deduct score from player (for upgrade purchases)
        /// </summary>
        [HttpPost("deduct-score")]
        public async Task<ActionResult> DeductScore([FromBody] DeductScoreRequest request)
        {
            _logger.LogRequestStart(_correlationService, "DeductScore");

            try
            {
                var session = await _gameEngine.GetGameSessionAsync(request.PlayerId);
                var amount = new BigNumber(decimal.Parse(request.Amount));

                if (session.Score < amount)
                {
                    _logger.LogBusinessEvent(_correlationService, "ScoreDeductionFailed", new
                    {
                        PlayerId = request.PlayerId,
                        RequestedAmount = request.Amount,
                        CurrentScore = session.Score.ToString(),
                        Reason = "Insufficient funds"
                    });

                    return BadRequest(new { error = "Insufficient score" });
                }

                // Deduct the score
                session.Score = session.Score - amount;
                await _gameEngine.SaveGameSessionAsync(session);

                _logger.LogBusinessEvent(_correlationService, "ScoreDeducted", new
                {
                    PlayerId = request.PlayerId,
                    DeductedAmount = request.Amount,
                    RemainingScore = session.Score.ToString(),
                    Reason = request.Reason
                });

                return Ok(new
                {
                    success = true,
                    remainingScore = session.Score.ToString(),
                    deductedAmount = request.Amount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error deducting score for player {PlayerId}", request.PlayerId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Apply upgrade effects to player
        /// </summary>
        [HttpPost("apply-upgrade-effects")]
        public async Task<ActionResult> ApplyUpgradeEffects([FromBody] ApplyUpgradeEffectsRequest request)
        {
            _logger.LogRequestStart(_correlationService, "ApplyUpgradeEffects");

            try
            {
                var session = await _gameEngine.GetGameSessionAsync(request.PlayerId);

                // Apply click power bonuses
                if (request.ClickPowerBonus > 0)
                {
                    session.ClickPower = session.ClickPower + new BigNumber(request.ClickPowerBonus);
                }

                // Apply passive income bonuses
                if (request.PassiveIncomeBonus > 0)
                {
                    session.PassiveIncomePerSecond += request.PassiveIncomeBonus;
                }

                await _gameEngine.SaveGameSessionAsync(session);

                _logger.LogBusinessEvent(_correlationService, "UpgradeEffectsApplied", new
                {
                    PlayerId = request.PlayerId,
                    ClickPowerBonus = request.ClickPowerBonus,
                    PassiveIncomeBonus = request.PassiveIncomeBonus,
                    NewClickPower = session.ClickPower.ToString(),
                    NewPassiveIncome = session.PassiveIncomePerSecond
                });

                return Ok(new
                {
                    success = true,
                    newClickPower = session.ClickPower.ToString(),
                    newPassiveIncome = session.PassiveIncomePerSecond
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error applying upgrade effects for player {PlayerId}", request.PlayerId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("session/create")]
        public async Task<ActionResult<GameSessionDto>> CreateGameSession()
        {
            _logger.LogRequestStart(_correlationService, "CreateGameSession");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var username = GetUsernameFromToken();

                _logger.LogBusinessEvent(_correlationService, "GameSessionCreating", new { PlayerId = playerId, Username = username });

                var session = await _gameEngine.CreateGameSessionAsync(playerId, username);

                _logger.LogBusinessEvent(_correlationService, "GameSessionCreated", new
                {
                    PlayerId = playerId,
                    SessionId = session.SessionId,
                    Username = username
                });

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
                _logger.LogError(_correlationService, ex, "Error creating game session");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("offline-earnings")]
        public async Task<ActionResult<OfflineEarningsDto>> GetOfflineEarnings()
        {
            _logger.LogRequestStart(_correlationService, "GetOfflineEarnings");

            try
            {
                var playerId = GetPlayerIdFromToken();
                var earnings = await _gameEngine.CalculateOfflineEarningsAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "OfflineEarningsCalculated", new
                {
                    PlayerId = playerId,
                    OfflineEarnings = earnings.ToString()
                });

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
                _logger.LogError(_correlationService, ex, "Error calculating offline earnings");
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

    // Request/Response DTOs for new endpoints
    public class DeductScoreRequest
    {
        public Guid PlayerId { get; set; }
        public string Amount { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class ApplyUpgradeEffectsRequest
    {
        public Guid PlayerId { get; set; }
        public decimal ClickPowerBonus { get; set; }
        public decimal PassiveIncomeBonus { get; set; }
        public decimal MultiplierBonus { get; set; } = 1.0m;
        public string SourceUpgradeId { get; set; } = string.Empty;
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