using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.DTOs.Notifications
{
    public class ScoreNotificationDto : BaseNotificationDto
    {
        public ScoreNotificationDto()
        {
            Type = NotificationType.Score;
        }

        public Guid PlayerId { get; init; }
        public string CurrentScore { get; init; } = string.Empty;
        public string PreviousScore { get; init; } = string.Empty;
        public string EarnedAmount { get; init; } = string.Empty;
        public long ClickCount { get; init; }
        public string ClickPower { get; init; } = string.Empty;
        public decimal PassiveIncome { get; init; }
        public string? Source { get; init; } // "click", "passive", "upgrade", etc.
    }
}