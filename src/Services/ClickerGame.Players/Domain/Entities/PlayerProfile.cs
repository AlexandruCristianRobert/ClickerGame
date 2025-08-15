using System.ComponentModel.DataAnnotations;

namespace ClickerGame.Players.Domain.Entities
{
    public class PlayerProfile
    {
        [Key]
        public Guid ProfileId { get; set; }
        public Guid PlayerId { get; set; }

        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Avatar { get; set; }
        public int TotalPlayTimeMinutes { get; set; }
        public DateTime? LastActiveAt { get; set; }

        public Player Player { get; set; } = null!;
    }
}
