using ClickerGame.GameCore.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace ClickerGame.GameCore.Domain.Entities
{
    public class GameSession
    {
        [Key]
        public Guid SessionId { get; set; }
        public Guid PlayerId { get; set; }
        public string PlayerUsername { get; set; } = string.Empty;
        public BigNumber Score { get; set; }
        public long ClickCount { get; set; }
        public BigNumber ClickPower { get; set; } = BigNumber.One;
        public decimal PassiveIncomePerSecond { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public DateTime? LastClickTime { get; set; }
        public bool IsActive { get; set; }
        public string GameStateJson { get; set; } = "{}";

        public int ClicksInLastMinute { get; set; }
        public DateTime LastAntiCheatCheck { get; set; }

        public BigNumber ProcessClick(BigNumber clickPower)
        {
            var now = DateTime.UtcNow;

            if (now - LastAntiCheatCheck > TimeSpan.FromMinutes(1))
            {
                ClicksInLastMinute = 0;
                LastAntiCheatCheck = now;
            }

            if (ClicksInLastMinute >= 1000)
            {
                throw new InvalidOperationException("Click rate limit exceeded");
            }

            var earnedValue = clickPower;
            Score += earnedValue;
            ClickCount++;
            ClicksInLastMinute++;
            LastClickTime = now;
            LastUpdateTime = now;

            return earnedValue;
        }

        public BigNumber CalculateOfflineEarnings()
        {
            if (LastUpdateTime == default || PassiveIncomePerSecond <= 0)
                return BigNumber.Zero;

            var secondsOffline = (DateTime.UtcNow - LastUpdateTime).TotalSeconds;
            var maxOfflineHours = 24;
            var cappedSeconds = Math.Min(secondsOffline, maxOfflineHours * 3600);

            var offlineEarnings = new BigNumber((decimal)(PassiveIncomePerSecond * cappedSeconds));
            Score += offlineEarnings;
            LastUpdateTime = DateTime.UtcNow;

            return offlineEarnings;
        }
    }
}