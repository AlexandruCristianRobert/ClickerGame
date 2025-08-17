using ClickerGame.GameCore.Application.DTOs.Notifications;
using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Domain.Events
{
    public abstract class BaseNotificationEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string CorrelationId { get; init; } = string.Empty;
        public NotificationType NotificationType { get; init; }
        public NotificationTargetType TargetType { get; init; }
    }

    public class ScoreUpdatedEvent : BaseNotificationEvent
    {
        public ScoreUpdatedEvent()
        {
            NotificationType = NotificationType.Score;
            TargetType = NotificationTargetType.Individual;
        }

        public ScoreNotificationDto Notification { get; init; } = new();
    }

    public class AchievementUnlockedEvent : BaseNotificationEvent
    {
        public AchievementUnlockedEvent()
        {
            NotificationType = NotificationType.Achievement;
            TargetType = NotificationTargetType.Individual;
        }

        public AchievementNotificationDto Notification { get; init; } = new();
    }

    public class UpgradePurchasedEvent : BaseNotificationEvent
    {
        public UpgradePurchasedEvent()
        {
            NotificationType = NotificationType.Upgrade;
            TargetType = NotificationTargetType.Individual;
        }

        public UpgradeNotificationDto Notification { get; init; } = new();
    }

    public class SystemEventTriggeredEvent : BaseNotificationEvent
    {
        public SystemEventTriggeredEvent()
        {
            NotificationType = NotificationType.System;
            TargetType = NotificationTargetType.Broadcast;
        }

        public SystemNotificationDto Notification { get; init; } = new();
    }

    public class PlayerPresenceChangedEvent : BaseNotificationEvent
    {
        public PlayerPresenceChangedEvent()
        {
            NotificationType = NotificationType.Presence;
            TargetType = NotificationTargetType.Broadcast;
        }

        public PresenceNotificationDto Notification { get; init; } = new();
    }

    public class GameEventStartedEvent : BaseNotificationEvent
    {
        public GameEventStartedEvent()
        {
            NotificationType = NotificationType.Event;
            TargetType = NotificationTargetType.Broadcast;
        }

        public EventNotificationDto Notification { get; init; } = new();
    }
}