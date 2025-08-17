using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.DTOs.Notifications
{
    public abstract class BaseNotificationDto
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public NotificationType Type { get; init; }
        public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public TimeSpan? DisplayDuration { get; init; }
        public bool RequiresUserAction { get; init; } = false;
        public Dictionary<string, object> Metadata { get; init; } = new();
        public Guid? TargetPlayerId { get; init; }
        public NotificationTargetType TargetType { get; init; } = NotificationTargetType.Individual;
    }
}