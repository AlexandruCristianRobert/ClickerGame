using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.DTOs.Notifications
{
    public class UpgradeNotificationDto : BaseNotificationDto
    {
        public UpgradeNotificationDto()
        {
            Type = NotificationType.Upgrade;
            Priority = NotificationPriority.Normal;
            DisplayDuration = TimeSpan.FromSeconds(5);
        }

        public Guid PlayerId { get; init; }
        public string UpgradeId { get; init; } = string.Empty;
        public string UpgradeName { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public int NewLevel { get; init; }
        public int PreviousLevel { get; init; }
        public string TotalCost { get; init; } = string.Empty;
        public string? IconUrl { get; init; }
        public Dictionary<string, string> EffectChanges { get; init; } = new();
        public bool IsMaxLevel { get; init; } = false;
    }
}