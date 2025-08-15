using ClickerGame.Players.Application.DTOs;
using ClickerGame.Players.Application.Services;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ClickerGame.Players.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlayersController : ControllerBase
    {
        private readonly IPlayerService _playerService;
        private readonly ILogger<PlayersController> _logger;
        private readonly ICorrelationService _correlationService;

        public PlayersController(
            IPlayerService playerService,
            ILogger<PlayersController> logger,
            ICorrelationService correlationService)
        {
            _playerService = playerService;
            _logger = logger;
            _correlationService = correlationService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<TokenResponseDto>> Register([FromBody] RegisterPlayerDto dto)
        {
            _logger.LogRequestStart(_correlationService, "RegisterPlayer");

            try
            {
                _logger.LogBusinessEvent(_correlationService, "PlayerRegistrationAttempt", new { dto.Username, dto.Email });

                var result = await _playerService.RegisterAsync(dto);

                _logger.LogBusinessEvent(_correlationService, "PlayerRegistrationSuccess", new { dto.Username, PlayerId = result.Player.PlayerId });

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogBusinessEvent(_correlationService, "PlayerRegistrationFailed", new { dto.Username, dto.Email, Reason = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error during player registration for {Username}", dto.Username);
                return StatusCode(500, new { error = "An error occurred during registration" });
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<TokenResponseDto>> Login([FromBody] LoginDto dto)
        {
            _logger.LogRequestStart(_correlationService, "LoginPlayer");

            try
            {
                _logger.LogBusinessEvent(_correlationService, "PlayerLoginAttempt", new { dto.Username });

                var result = await _playerService.LoginAsync(dto);

                _logger.LogBusinessEvent(_correlationService, "PlayerLoginSuccess", new { dto.Username, PlayerId = result.Player.PlayerId });

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogBusinessEvent(_correlationService, "PlayerLoginFailed", new { dto.Username, Reason = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogBusinessEvent(_correlationService, "PlayerLoginUnauthorized", new { dto.Username, Reason = ex.Message });
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error during login for {Username}", dto.Username);
                return StatusCode(500, new { error = "An error occurred during login" });
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<TokenResponseDto>> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            _logger.LogRequestStart(_correlationService, "RefreshToken");

            var result = await _playerService.RefreshTokenAsync(dto.RefreshToken);
            if (result == null)
            {
                _logger.LogBusinessEvent(_correlationService, "TokenRefreshFailed", new { Reason = "Invalid or expired token" });
                return Unauthorized(new { error = "Invalid or expired refresh token" });
            }

            _logger.LogBusinessEvent(_correlationService, "TokenRefreshSuccess", new { PlayerId = result.Player.PlayerId });
            return Ok(result);
        }

        [HttpPost("revoke")]
        [Authorize]
        public async Task<ActionResult> RevokeToken([FromBody] RefreshTokenDto dto)
        {
            _logger.LogRequestStart(_correlationService, "RevokeToken");

            var success = await _playerService.RevokeTokenAsync(dto.RefreshToken);

            if (!success)
            {
                _logger.LogBusinessEvent(_correlationService, "TokenRevokeFailed", new { Reason = "Token not found" });
                return NotFound(new { error = "Token not found" });
            }

            _logger.LogBusinessEvent(_correlationService, "TokenRevokeSuccess", null);
            return NoContent();
        }

        [HttpGet("{playerId:guid}")]
        [Authorize]
        public async Task<ActionResult<PlayerDto>> GetPlayer(Guid playerId)
        {
            _logger.LogRequestStart(_correlationService, "GetPlayerById");

            var player = await _playerService.GetPlayerByIdAsync(playerId);
            if (player == null)
            {
                _logger.LogBusinessEvent(_correlationService, "PlayerNotFound", new { PlayerId = playerId });
                return NotFound(new { error = "Player not found" });
            }

            _logger.LogBusinessEvent(_correlationService, "PlayerRetrieved", new { PlayerId = playerId });
            return Ok(player);
        }

        [HttpGet("username/{username}")]
        [Authorize]
        public async Task<ActionResult<PlayerDto>> GetPlayerByUsername(string username)
        {
            _logger.LogRequestStart(_correlationService, "GetPlayerByUsername");

            var player = await _playerService.GetPlayerByUsernameAsync(username);
            if (player == null)
            {
                _logger.LogBusinessEvent(_correlationService, "PlayerNotFoundByUsername", new { Username = username });
                return NotFound(new { error = "Player not found" });
            }

            _logger.LogBusinessEvent(_correlationService, "PlayerRetrievedByUsername", new { Username = username, PlayerId = player.PlayerId });
            return Ok(player);
        }

        [HttpPut("{playerId:guid}/profile")]
        [Authorize]
        public async Task<ActionResult> UpdateProfile(Guid playerId, [FromBody] UpdateProfileDto dto)
        {
            _logger.LogRequestStart(_correlationService, "UpdatePlayerProfile");

            var success = await _playerService.UpdateProfileAsync(playerId, dto.DisplayName, dto.Avatar);
            if (!success)
            {
                _logger.LogBusinessEvent(_correlationService, "ProfileUpdateFailed", new { PlayerId = playerId, Reason = "Profile not found" });
                return NotFound(new { error = "Profile not found" });
            }

            _logger.LogBusinessEvent(_correlationService, "ProfileUpdateSuccess", new { PlayerId = playerId, DisplayName = dto.DisplayName });
            return NoContent();
        }

        [HttpGet("health")]
        public ActionResult GetHealth()
        {
            return Ok(new { status = "Healthy", service = "Player Service", timestamp = DateTime.UtcNow });
        }
    }

    public class RefreshTokenDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UpdateProfileDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
    }
}