using System.ComponentModel.DataAnnotations;

namespace ClickerGame.GameCore.Domain.Entities
{
    public class GameState
    {
        [Key]
        public Guid StateId { get; set; }
        public Guid PlayerId { get; set; }
        public long CurrentScore { get; set; }
        public int ClickPower { get; set; } = 1;
        public decimal PassiveIncomePerSecond { get; set; }
        public DateTime LastSaveTime { get; set; }
    }
}
