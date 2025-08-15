using ClickerGame.Players.Application.DTOs;
using ClickerGame.Players.Domain.Entities;
using ClickerGame.Players.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClickerGame.Players.Application.Services
{
    public class PlayerService : IPlayerService
    {
        private readonly PlayersDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly ILogger<PlayerService> _logger;

        public PlayerService(PlayersDbContext context, IJwtService jwtService, ILogger<PlayerService> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task<TokenResponseDto> RegisterAsync(RegisterPlayerDto dto)
        {
            if(await _context.Players.AnyAsync(p => p.Username == dto.Username || p.Email == dto.Email))
            {
                throw new InvalidOperationException("Username or email already exists");
            }

            var player = new Player
            {
                PlayerId = Guid.NewGuid(),
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsActive = true,
            };

            var profile = new PlayerProfile 
            {
                ProfileId = Guid.NewGuid(),
                PlayerId = player.PlayerId,
                DisplayName = dto.DisplayName ?? dto.Username,
                TotalPlayTimeMinutes = 0,
                LastActiveAt = DateTime.UtcNow
            };

            player.Profile = profile;

            var accessToken = _jwtService.GenerateAccessToken(player);
            var refreshToken = _jwtService.GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                PlayerId = player.PlayerId,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
            };

            player.RefreshTokens.Add(refreshTokenEntity);

            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New player registered: {Username}", player.Username);

            return new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Player = MapToDto(player)
            };
        }

        public async Task<TokenResponseDto> LoginAsync(LoginDto dto)
        {
            var player = await _context.Players
                .Include(p => p.Profile)
                .FirstOrDefaultAsync(p => p.Username == dto.Username);

            if(player is null || !BCrypt.Net.BCrypt.Verify(dto.Password, player.PasswordHash))
            {
                throw new InvalidOperationException("Invalid username or password");
            }

            if (!player.IsActive)
            {
                throw new UnauthorizedAccessException("Account is deactivated");



            }

            player.LastLoginAt = DateTime.UtcNow;
            
            var accessToken = _jwtService.GenerateAccessToken(player);
            var refreshToken = _jwtService.GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                PlayerId = player.PlayerId,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Player logged in: {Username}", player.Username);

            return new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Player = MapToDto(player)
            };
        }

        public async Task<PlayerDto?> GetPlayerByIdAsync(Guid playerId)
        {
            var player = await _context.Players
                .Include(p => p.Profile)
                .FirstOrDefaultAsync(p => p.PlayerId == playerId);

            return player != null ? MapToDto(player) : null;
        }

        public async Task<PlayerDto?> GetPlayerByUsernameAsync(string username)
        {
            var player = await _context.Players
                .Include(p => p.Profile)
                .FirstOrDefaultAsync(p => p.Username == username);

            return player != null ? MapToDto(player) : null;
        }

        public async Task<bool> UpdateProfileAsync(Guid playerId, string displayName, string? avatar)
        {
            var profile = await _context.PlayerProfiles
                .FirstOrDefaultAsync(p => p.PlayerId == playerId);

            if (profile == null)
                return false;

            profile.DisplayName = displayName;
            profile.Avatar = avatar;
            profile.LastActiveAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<TokenResponseDto?> RefreshTokenAsync(string refreshToken)
        {
            var tokenEntity = await _context.RefreshTokens
                .Include(r => r.Player)
                .ThenInclude(p => p.Profile)
                .FirstOrDefaultAsync(r => r.Token == refreshToken && !r.IsRevoked);

            if(tokenEntity == null || tokenEntity.ExpiresAt < DateTime.UtcNow)
            {
                return null;
            }

            tokenEntity.IsRevoked = true;

            var accessToken = _jwtService.GenerateAccessToken(tokenEntity.Player);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            var newRefreshTokenEntity = new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                PlayerId = tokenEntity.PlayerId,
                Token = newRefreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            };

            _context.RefreshTokens.Add(newRefreshTokenEntity);
            await _context.SaveChangesAsync();

            return new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Player = MapToDto(tokenEntity.Player)
            };
        }

        public async Task<bool> RevokeTokenAsync(string refreshToken)
        {
            var tokenEntity = await _context.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == refreshToken);

            if (tokenEntity == null)
                return false;

            tokenEntity.IsRevoked = true;
            await _context.SaveChangesAsync();

            return true;
        }

        private PlayerDto MapToDto(Player player)
        {
            return new PlayerDto
            {
                PlayerId = player.PlayerId,
                Username = player.Username,
                Email = player.Email,
                DisplayName = player.Profile?.DisplayName ?? player.Username,
                Avatar = player.Profile?.Avatar,
                CreatedAt = player.CreatedAt,
                LastLoginAt = player.LastLoginAt
            };
        }
    }
}
