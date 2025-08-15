using ClickerGame.Players.Application.DTOs;
using ClickerGame.Players.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.Intrinsics.X86;

namespace ClickerGame.Players.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlayersController : ControllerBase
    {
        private readonly IPlayerService _playerService;
        private readonly ILogger<PlayersController> _logger;

        public PlayersController(IPlayerService playerService, ILogger<PlayersController> logger)
        {
            _playerService = playerService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<ActionResult<TokenResponseDto>> Register([FromBody] RegisterPlayerDto dto)
        {
            try
            {
                var result = await _playerService.RegisterAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durring registration");
                return StatusCode(500, new { error = "An error occured during registration" });
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<TokenResponseDto>> Login([FromBody] LoginDto dto)
        {
            try
            {
                var result = await _playerService.LoginAsync(dto);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { error = "An error occurred during login" });
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<TokenResponseDto>> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var result = await _playerService.RefreshTokenAsync(dto.RefreshToken);
            if(result == null)
                return Unauthorized(new { error = "Invalid or expired refresh token" });

            return Ok(result);
        }

        [HttpPost("revoke")]
        [Authorize]
        public async Task<ActionResult> RevokeToken([FromBody] RefreshTokenDto dto)
        {
           var success = await _playerService.RevokeTokenAsync(dto.RefreshToken);

            if (!success)
                return NotFound(new { error = "Token not found" });

            return NoContent();
        }

        [HttpGet("{playerId:guid}")]
        [Authorize]
        public async Task<ActionResult<PlayerDto>> GetPlayer(Guid playerId)
        {
            var player = await _playerService.GetPlayerByIdAsync(playerId);
            if (player == null)
                return NotFound(new { error = "Player not found" });

            return Ok(player);
        }

        [HttpGet("username/{username}")]
        [Authorize]
        public async Task<ActionResult<PlayerDto>> GetPlayerByUsername(string username)
        {
            var player = await _playerService.GetPlayerByUsernameAsync(username);
            if (player == null)
                return NotFound(new { error = "Player not found" });

            return Ok(player);
        }

        [HttpPut("{playerId:guid}/profile")]
        [Authorize]
        public async Task<ActionResult> UpdateProfile(Guid playerId, [FromBody] UpdateProfileDto dto)
        {
            var success = await _playerService.UpdateProfileAsync(playerId, dto.DisplayName, dto.Avatar);
            if(!success)
                return NotFound(new { error = "Profile not found" });

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
