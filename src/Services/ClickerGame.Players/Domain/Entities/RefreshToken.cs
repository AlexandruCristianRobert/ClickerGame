using System.ComponentModel.DataAnnotations;

namespace ClickerGame.Players.Domain.Entities
{
    public class RefreshToken
    {
        [Key]
        public Guid TokenId { get; set; }
        public Guid PlayerId { get; set; }
        [Required]
        public string Token { get; set; } = string.Empty;


        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRevoked { get; set; }

        public Player Player { get; set; } = null!;
    }
}
