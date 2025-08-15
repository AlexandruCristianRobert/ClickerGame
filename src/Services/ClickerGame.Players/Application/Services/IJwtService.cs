using ClickerGame.Players.Domain.Entities;

namespace ClickerGame.Players.Application.Services
{
    public interface IJwtService
    {
        string GenerateAccessToken(Player player);
        string GenerateRefreshToken();
        bool ValidateToken(string token);
    }
}
