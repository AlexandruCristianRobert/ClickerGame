using System.ComponentModel.DataAnnotations;

namespace ClickerGame.GameCore.Domain.Entities
{
    public class GameSession
    {
        [Key]
        public Guid SessionId { get; set; }
        public Guid PlayerId { get; set; }
        public long Score { get; set; }
        public long ClickCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public bool IsActive { get; set; }
    }
}
