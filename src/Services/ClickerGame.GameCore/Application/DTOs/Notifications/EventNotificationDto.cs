using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.DTOs.Notifications
{
    public class EventNotificationDto : BaseNotificationDto
    {
        public EventNotificationDto()
        {
            Type = NotificationType.Event;
            TargetType = NotificationTargetType.Broadcast;
        }

        public string EventName { get; init; } = string.Empty;
        public string EventType { get; init; } = string.Empty; // "golden_cookie", "bonus_event", "competition"
        public DateTime? StartTime { get; init; }
        public DateTime? EndTime { get; init; }
        public TimeSpan? Duration { get; init; }
        public Dictionary<string, object> EventData { get; init; } = new();
        public string? EventIconUrl { get; init; }
        public bool IsActive { get; init; } = true;
    }
}