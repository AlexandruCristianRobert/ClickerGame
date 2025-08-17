using ClickerGame.GameCore.Application.DTOs.Notifications;

namespace ClickerGame.GameCore.Application.Services
{
    public interface ISystemEventService
    {
        // Global Announcements
        Task BroadcastAnnouncementAsync(AnnouncementNotificationDto announcement);
        Task BroadcastSystemMessageAsync(string message, SystemEventSeverity severity = SystemEventSeverity.Info);
        Task ScheduleAnnouncementAsync(AnnouncementNotificationDto announcement, DateTime scheduledTime);

        // Maintenance Notifications
        Task BroadcastMaintenanceNotificationAsync(MaintenanceNotificationDto maintenance);
        Task UpdateMaintenancePhaseAsync(string maintenanceId, MaintenancePhase phase);
        Task CancelMaintenanceAsync(string maintenanceId, string reason);

        // Event Countdown Timers
        Task StartEventCountdownAsync(EventCountdownNotificationDto countdown);
        Task UpdateEventCountdownAsync(string eventId, TimeSpan timeRemaining);
        Task BroadcastEventStartedAsync(string eventId, string eventName);
        Task BroadcastEventEndedAsync(string eventId, string eventName);

        // Golden Cookie/Special Events
        Task SpawnGoldenCookieAsync(GoldenCookieNotificationDto goldenCookie);
        Task SpawnGoldenCookieForPlayerAsync(Guid playerId, GoldenCookieNotificationDto goldenCookie);
        Task BroadcastSpecialBonusEventAsync(string eventName, Dictionary<string, object> eventData);

        // System Status
        Task BroadcastSystemStatusAsync(string status, Dictionary<string, object> statusData);
        Task BroadcastEmergencyNotificationAsync(string message, string actionRequired);

        // Event Management
        Task<IEnumerable<SystemNotificationDto>> GetActiveSystemEventsAsync();
        Task<IEnumerable<SystemNotificationDto>> GetScheduledEventsAsync(DateTime fromDate, DateTime toDate);
        Task AcknowledgeSystemEventAsync(Guid playerId, string eventId);
        Task<bool> HasPlayerAcknowledgedEventAsync(Guid playerId, string eventId);
    }
}