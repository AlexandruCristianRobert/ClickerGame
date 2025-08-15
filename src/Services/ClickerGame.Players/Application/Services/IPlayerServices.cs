using ClickerGame.Players.Application.DTOs;

namespace ClickerGame.Players.Application.Services
{
    public interface IPlayerService
    {
        Task<TokenResponseDto> RegisterAsync(RegisterPlayerDto dto);
        Task<TokenResponseDto> LoginAsync(LoginDto dto);
        Task<PlayerDto?> GetPlayerByIdAsync(Guid playerId);
        Task<PlayerDto?> GetPlayerByUsernameAsync(string username);
        Task<bool> UpdateProfileAsync(Guid playerId, string displayName, string? avatar);
        Task<TokenResponseDto> RefreshTokenAsync(string refreshToken);
        Task<bool> RevokeTokenAsync(string refreshToken);
    }
}
