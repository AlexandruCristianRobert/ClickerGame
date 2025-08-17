using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.DTOs.Notifications
{
    public class AchievementProgressNotificationDto : BaseNotificationDto
    {
        public AchievementProgressNotificationDto()
        {
            Type = NotificationType.Achievement;
            Priority = NotificationPriority.Low;
            DisplayDuration = TimeSpan.FromSeconds(3);
            RequiresUserAction = false;
        }

        public Guid PlayerId { get; init; }
        public string AchievementId { get; init; } = string.Empty;
        public string AchievementName { get; init; } = string.Empty;
        public int CurrentProgress { get; init; }
        public int RequiredProgress { get; init; }
        public decimal ProgressPercentage { get; init; }
        public bool IsNearCompletion { get; init; } = false; // 90%+ progress
        public string? MilestoneReached { get; init; } // "25%", "50%", "75%"
        public List<string> RecentActions { get; init; } = new(); // Actions that contributed to progress
    }
}