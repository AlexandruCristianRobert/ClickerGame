using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.DTOs.Notifications
{
    public class SystemNotificationDto : BaseNotificationDto
    {
        public SystemNotificationDto()
        {
            Type = NotificationType.System;
            TargetType = NotificationTargetType.Broadcast;
        }

        public string SystemEvent { get; init; } = string.Empty;
        public string? ActionUrl { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public bool IsMaintenance { get; init; } = false;
        public string? MaintenanceMessage { get; init; }

        // Enhanced system event properties
        public SystemEventType EventType { get; init; } = SystemEventType.Announcement;
        public SystemEventSeverity Severity { get; init; } = SystemEventSeverity.Info;
        public bool RequiresAcknowledgment { get; init; } = false;
        public Dictionary<string, object> EventData { get; init; } = new();
        public List<string> AffectedServices { get; init; } = new();
        public string? ContactInfo { get; init; }
        public bool IsScheduled { get; init; } = false;
        public DateTime? ScheduledStartTime { get; init; }
        public DateTime? ScheduledEndTime { get; init; }
        public TimeSpan? EstimatedDuration { get; init; }
        public List<SystemEventAction> AvailableActions { get; init; } = new();
    }

    public class MaintenanceNotificationDto : SystemNotificationDto
    {
        public MaintenanceNotificationDto()
        {
            EventType = SystemEventType.Maintenance;
            IsMaintenance = true;
            RequiresAcknowledgment = true;
            Severity = SystemEventSeverity.Warning;
            Priority = NotificationPriority.High;
            DisplayDuration = TimeSpan.FromMinutes(1);
        }

        public MaintenancePhase Phase { get; init; } = MaintenancePhase.Scheduled;
        public TimeSpan? TimeUntilMaintenance { get; init; }
        public List<string> AffectedFeatures { get; init; } = new();
        public string? AlternativeAction { get; init; }
        public bool AllowsContinuedPlay { get; init; } = false;
    }

    public class EventCountdownNotificationDto : SystemNotificationDto
    {
        public EventCountdownNotificationDto()
        {
            EventType = SystemEventType.SpecialEvent;
            Priority = NotificationPriority.Normal;
            DisplayDuration = TimeSpan.FromSeconds(5);
        }

        public string CountdownEventId { get; init; } = string.Empty;
        public DateTime EventStartTime { get; init; }
        public DateTime EventEndTime { get; init; }
        public TimeSpan TimeRemaining { get; init; }
        public string EventDescription { get; init; } = string.Empty;
        public List<EventReward> EventRewards { get; init; } = new();
        public bool IsStartingSoon { get; init; } = false;
        public bool IsEndingSoon { get; init; } = false;
        public string? EventImageUrl { get; init; }
    }

    public class GoldenCookieNotificationDto : SystemNotificationDto
    {
        public GoldenCookieNotificationDto()
        {
            EventType = SystemEventType.GoldenCookie;
            Priority = NotificationPriority.High;
            DisplayDuration = TimeSpan.FromSeconds(3);
            RequiresUserAction = true;
        }

        public string CookieId { get; init; } = string.Empty;
        public TimeSpan AvailableDuration { get; init; }
        public GoldenCookieType CookieType { get; init; } = GoldenCookieType.Standard;
        public string RewardDescription { get; init; } = string.Empty;
        public decimal MultiplierBonus { get; init; } = 1.0m;
        public string ClickPowerBonus { get; init; } = string.Empty;
        public bool IsRare { get; init; } = false;
        public DateTime ExpiresAt { get; init; }
        public string? SpecialEffect { get; init; }
    }

    public class AnnouncementNotificationDto : SystemNotificationDto
    {
        public AnnouncementNotificationDto()
        {
            EventType = SystemEventType.Announcement;
            Priority = NotificationPriority.Normal;
            DisplayDuration = TimeSpan.FromSeconds(8);
        }

        public string AnnouncementId { get; init; } = string.Empty;
        public AnnouncementCategory Category { get; init; } = AnnouncementCategory.General;
        public List<string> Tags { get; init; } = new();
        public string? ImageUrl { get; init; }
        public string? VideoUrl { get; init; }
        public bool IsPinned { get; init; } = false;
        public int? DisplayOrder { get; init; }
        public List<string> TargetPlayerSegments { get; init; } = new();
    }

    // Supporting enums and classes
    public enum SystemEventType
    {
        Announcement = 1,
        Maintenance = 2,
        SpecialEvent = 3,
        GoldenCookie = 4,
        SeasonalEvent = 5,
        Competition = 6,
        Update = 7,
        Emergency = 8,
        Celebration = 9
    }

    public enum SystemEventSeverity
    {
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    public enum MaintenancePhase
    {
        Scheduled = 1,
        Warning = 2,
        Imminent = 3,
        InProgress = 4,
        Completed = 5,
        Extended = 6
    }

    public enum GoldenCookieType
    {
        Standard = 1,
        Lucky = 2,
        Frenzy = 3,
        ClickFrenzy = 4,
        Multiply = 5,
        Rare = 6,
        Legendary = 7
    }

    public enum AnnouncementCategory
    {
        General = 1,
        GameUpdate = 2,
        Event = 3,
        Community = 4,
        Competition = 5,
        Maintenance = 6,
        Celebration = 7
    }

    public class SystemEventAction
    {
        public string ActionId { get; init; } = string.Empty;
        public string ActionText { get; init; } = string.Empty;
        public string ActionUrl { get; init; } = string.Empty;
        public bool IsDestructive { get; init; } = false;
        public bool IsPrimary { get; init; } = false;
    }

    public class EventReward
    {
        public string RewardType { get; init; } = string.Empty; // "score", "multiplier", "achievement", "cosmetic"
        public string RewardAmount { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string? IconUrl { get; init; }
        public bool IsGuaranteed { get; init; } = true;
        public decimal DropChance { get; init; } = 1.0m;
    }
}