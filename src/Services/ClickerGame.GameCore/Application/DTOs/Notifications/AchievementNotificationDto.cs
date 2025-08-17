using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.DTOs.Notifications
{
    public class AchievementNotificationDto : BaseNotificationDto
    {
        public AchievementNotificationDto()
        {
            Type = NotificationType.Achievement;
            Priority = NotificationPriority.High;
            DisplayDuration = TimeSpan.FromSeconds(10);
            RequiresUserAction = true;
        }

        public Guid PlayerId { get; init; }
        public string AchievementId { get; init; } = string.Empty;
        public string AchievementName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Rarity { get; init; } = string.Empty;
        public string? IconUrl { get; init; }
        public string? RewardType { get; init; }
        public string? RewardAmount { get; init; }
        public int Points { get; init; }
        public bool IsFirstTime { get; init; } = true;

        // Enhanced properties for better achievement notifications
        public DateTime? UnlockedAt { get; init; } = DateTime.UtcNow;
        public string? BadgeColor { get; init; }
        public int? ProgressPercentage { get; init; }
        public List<AchievementReward> Rewards { get; init; } = new();
        public AchievementDifficulty Difficulty { get; init; } = AchievementDifficulty.Normal;
        public bool IsHidden { get; init; } = false;
        public List<string> Tags { get; init; } = new();
        public string? CelebrationAnimation { get; init; }
        public string? SoundEffect { get; init; }
        public bool ShowGlobalBroadcast { get; init; } = false;
        public AchievementStats? Stats { get; init; }
    }

    public class AchievementReward
    {
        public string Type { get; init; } = string.Empty; // "score", "clickPower", "multiplier", "cosmetic"
        public string Amount { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? IconUrl { get; init; }
    }

    public class AchievementStats
    {
        public int TotalUnlocked { get; init; }
        public int TotalAvailable { get; init; }
        public decimal CompletionPercentage { get; init; }
        public string NextAchievement { get; init; } = string.Empty;
        public int PointsEarned { get; init; }
        public string PlayerRank { get; init; } = string.Empty;
    }

    public enum AchievementDifficulty
    {
        Trivial = 1,
        Easy = 2,
        Normal = 3,
        Hard = 4,
        Extreme = 5,
        Legendary = 6
    }
}