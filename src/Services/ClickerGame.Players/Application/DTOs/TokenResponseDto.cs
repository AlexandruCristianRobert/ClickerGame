namespace ClickerGame.Players.Application.DTOs
{
    public class TokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public PlayerDto Player { get; set; } = null!;
    }
}
