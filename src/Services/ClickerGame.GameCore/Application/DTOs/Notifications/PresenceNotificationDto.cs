using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.DTOs.Notifications
{
    public class PresenceNotificationDto : BaseNotificationDto
    {
        public PresenceNotificationDto()
        {
            Type = NotificationType.Presence;
            Priority = NotificationPriority.Low;
            TargetType = NotificationTargetType.Broadcast;
        }

        public Guid PlayerId { get; init; }
        public string Username { get; init; } = string.Empty;
        public bool IsOnline { get; init; }
        public string? Status { get; init; } // "online", "offline", "idle", "busy"
        public int OnlinePlayerCount { get; init; }
    }
}