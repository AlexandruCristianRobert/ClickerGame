using ClickerGame.GameCore.Application.DTOs.Notifications;
using ClickerGame.GameCore.Domain.Enums;
using ClickerGame.GameCore.Hubs;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Text.Json;

namespace ClickerGame.GameCore.Application.Services
{
    public class SystemEventService : ISystemEventService
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly IGameNotificationService _notificationService;
        private readonly ILogger<SystemEventService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly IDatabase _cache;
        private readonly ISignalRConnectionManager _connectionManager;

        public SystemEventService(
            IHubContext<GameHub> hubContext,
            IGameNotificationService notificationService,
            ILogger<SystemEventService> logger,
            ICorrelationService correlationService,
            IConnectionMultiplexer redis,
            ISignalRConnectionManager connectionManager)
        {
            _hubContext = hubContext;
            _notificationService = notificationService;
            _logger = logger;
            _correlationService = correlationService;
            _cache = redis.GetDatabase();
            _connectionManager = connectionManager;
        }

        #region Global Announcements

        public async Task BroadcastAnnouncementAsync(AnnouncementNotificationDto announcement)
        {
            try
            {
                await _notificationService.BroadcastNotificationAsync(announcement);
                await StoreSystemEventAsync(announcement);

                _logger.LogBusinessEvent(_correlationService, "AnnouncementBroadcasted", new
                {
                    announcement.AnnouncementId,
                    announcement.Category,
                    announcement.IsPinned,
                    TargetSegments = announcement.TargetPlayerSegments.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting announcement {AnnouncementId}", announcement.AnnouncementId);
            }
        }

        public async Task BroadcastSystemMessageAsync(string message, SystemEventSeverity severity = SystemEventSeverity.Info)
        {
            try
            {
                var announcement = new AnnouncementNotificationDto
                {
                    AnnouncementId = Guid.NewGuid().ToString(),
                    Title = GetSeverityTitle(severity),
                    Message = message,
                    Category = AnnouncementCategory.General,
                    Severity = severity,
                    Priority = GetPriorityFromSeverity(severity),
                    DisplayDuration = GetDisplayDurationFromSeverity(severity)
                };

                await BroadcastAnnouncementAsync(announcement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting system message with severity {Severity}", severity);
            }
        }

        public async Task ScheduleAnnouncementAsync(AnnouncementNotificationDto announcement, DateTime scheduledTime)
        {
            try
            {
                var scheduledEvent = new
                {
                    Type = "Announcement",
                    ScheduledTime = scheduledTime,
                    Data = announcement
                };

                var key = $"scheduled_events:{scheduledTime:yyyyMMddHHmm}";
                await _cache.ListLeftPushAsync(key, JsonSerializer.Serialize(scheduledEvent));
                await _cache.KeyExpireAsync(key, TimeSpan.FromDays(30));

                _logger.LogInformation("Scheduled announcement {AnnouncementId} for {ScheduledTime}",
                    announcement.AnnouncementId, scheduledTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling announcement {AnnouncementId}", announcement.AnnouncementId);
            }
        }

        #endregion

        #region Maintenance Notifications

        public async Task BroadcastMaintenanceNotificationAsync(MaintenanceNotificationDto maintenance)
        {
            try
            {
                await _notificationService.BroadcastNotificationAsync(maintenance);
                await StoreSystemEventAsync(maintenance);

                // Send different messages based on maintenance phase
                var additionalMessage = maintenance.Phase switch
                {
                    MaintenancePhase.Scheduled => "Maintenance has been scheduled.",
                    MaintenancePhase.Warning => "Maintenance starting soon. Please save your progress.",
                    MaintenancePhase.Imminent => "Maintenance starting in a few minutes!",
                    MaintenancePhase.InProgress => "Maintenance is currently in progress.",
                    MaintenancePhase.Completed => "Maintenance completed successfully.",
                    MaintenancePhase.Extended => "Maintenance has been extended. Sorry for the inconvenience.",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(additionalMessage))
                {
                    await _hubContext.Clients.All.SendAsync("MaintenanceUpdate", new
                    {
                        Phase = maintenance.Phase.ToString(),
                        Message = additionalMessage,
                        EstimatedDuration = maintenance.EstimatedDuration?.ToString(),
                        AffectedFeatures = maintenance.AffectedFeatures,
                        Timestamp = DateTime.UtcNow
                    });
                }

                _logger.LogBusinessEvent(_correlationService, "MaintenanceNotificationSent", new
                {
                    maintenance.Phase,
                    maintenance.EstimatedDuration,
                    AffectedFeatures = maintenance.AffectedFeatures.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting maintenance notification for phase {Phase}", maintenance.Phase);
            }
        }

        public async Task UpdateMaintenancePhaseAsync(string maintenanceId, MaintenancePhase phase)
        {
            try
            {
                var maintenance = new MaintenanceNotificationDto
                {
                    SystemEvent = maintenanceId,
                    Phase = phase,
                    Title = $"Maintenance Update - {phase}",
                    Message = GetMaintenancePhaseMessage(phase)
                };

                await BroadcastMaintenanceNotificationAsync(maintenance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating maintenance phase {Phase} for {MaintenanceId}", phase, maintenanceId);
            }
        }

        public async Task CancelMaintenanceAsync(string maintenanceId, string reason)
        {
            try
            {
                var cancellation = new MaintenanceNotificationDto
                {
                    SystemEvent = maintenanceId,
                    Phase = MaintenancePhase.Completed,
                    Title = "Maintenance Cancelled",
                    Message = $"Scheduled maintenance has been cancelled. Reason: {reason}",
                    Severity = SystemEventSeverity.Info,
                    Priority = NotificationPriority.Normal
                };

                await BroadcastMaintenanceNotificationAsync(cancellation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling maintenance {MaintenanceId}", maintenanceId);
            }
        }

        #endregion

        #region Event Countdown Timers

        public async Task StartEventCountdownAsync(EventCountdownNotificationDto countdown)
        {
            try
            {
                await _notificationService.BroadcastNotificationAsync(countdown);
                await StoreSystemEventAsync(countdown);

                // Store countdown info for periodic updates
                var countdownKey = $"event_countdown:{countdown.CountdownEventId}";
                await _cache.HashSetAsync(countdownKey, new HashEntry[]
                {
                    new("eventId", countdown.CountdownEventId),
                    new("startTime", countdown.EventStartTime.ToString("O")),
                    new("endTime", countdown.EventEndTime.ToString("O")),
                    new("description", countdown.EventDescription),
                    new("isActive", true)
                });
                await _cache.KeyExpireAsync(countdownKey, countdown.EventEndTime - DateTime.UtcNow + TimeSpan.FromHours(1));

                _logger.LogBusinessEvent(_correlationService, "EventCountdownStarted", new
                {
                    countdown.CountdownEventId,
                    countdown.EventStartTime,
                    countdown.TimeRemaining
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting event countdown for {EventId}", countdown.CountdownEventId);
            }
        }

        public async Task UpdateEventCountdownAsync(string eventId, TimeSpan timeRemaining)
        {
            try
            {
                var countdownKey = $"event_countdown:{eventId}";
                var eventData = await _cache.HashGetAllAsync(countdownKey);

                if (eventData.Length == 0) return;

                var eventInfo = eventData.ToDictionary(x => x.Name!, x => x.Value!);

                await _hubContext.Clients.Group("GameEvents").SendAsync("EventCountdownUpdate", new
                {
                    EventId = eventId,
                    TimeRemaining = timeRemaining.ToString(),
                    Description = eventInfo.GetValueOrDefault("description", ""),
                    IsStartingSoon = timeRemaining.TotalMinutes <= 5,
                    IsEndingSoon = timeRemaining.TotalMinutes <= 15,
                    Timestamp = DateTime.UtcNow
                });

                // Send special notifications for important milestones
                if (timeRemaining.TotalMinutes == 5)
                {
                    await BroadcastSystemMessageAsync($"Event '{eventInfo.GetValueOrDefault("description", eventId)}' starting in 5 minutes!", SystemEventSeverity.Info);
                }
                else if (timeRemaining.TotalMinutes == 1)
                {
                    await BroadcastSystemMessageAsync($"Event '{eventInfo.GetValueOrDefault("description", eventId)}' starting in 1 minute!", SystemEventSeverity.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating event countdown for {EventId}", eventId);
            }
        }

        public async Task BroadcastEventStartedAsync(string eventId, string eventName)
        {
            try
            {
                var eventNotification = new EventCountdownNotificationDto
                {
                    CountdownEventId = eventId,
                    Title = "🎉 Event Started!",
                    Message = $"The event '{eventName}' has begun! Join now for rewards!",
                    EventDescription = eventName,
                    Priority = NotificationPriority.High,
                    DisplayDuration = TimeSpan.FromSeconds(10)
                };

                await _notificationService.BroadcastNotificationAsync(eventNotification);

                await _hubContext.Clients.Group("GameEvents").SendAsync("EventStarted", new
                {
                    EventId = eventId,
                    EventName = eventName,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogBusinessEvent(_correlationService, "EventStarted", new { EventId = eventId, EventName = eventName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting event started for {EventId}", eventId);
            }
        }

        public async Task BroadcastEventEndedAsync(string eventId, string eventName)
        {
            try
            {
                var eventNotification = new EventCountdownNotificationDto
                {
                    CountdownEventId = eventId,
                    Title = "Event Ended",
                    Message = $"The event '{eventName}' has concluded. Thanks for participating!",
                    EventDescription = eventName,
                    Priority = NotificationPriority.Normal,
                    DisplayDuration = TimeSpan.FromSeconds(8)
                };

                await _notificationService.BroadcastNotificationAsync(eventNotification);

                // Clean up countdown data
                var countdownKey = $"event_countdown:{eventId}";
                await _cache.KeyDeleteAsync(countdownKey);

                _logger.LogBusinessEvent(_correlationService, "EventEnded", new { EventId = eventId, EventName = eventName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting event ended for {EventId}", eventId);
            }
        }

        #endregion

        #region Golden Cookie/Special Events

        public async Task SpawnGoldenCookieAsync(GoldenCookieNotificationDto goldenCookie)
        {
            try
            {
                await _notificationService.BroadcastNotificationAsync(goldenCookie);

                // Store golden cookie data for tracking
                var cookieKey = $"golden_cookie:{goldenCookie.CookieId}";
                await _cache.HashSetAsync(cookieKey, new HashEntry[]
                {
                    new("cookieId", goldenCookie.CookieId),
                    new("type", goldenCookie.CookieType.ToString()),
                    new("expiresAt", goldenCookie.ExpiresAt.ToString("O")),
                    new("multiplier", goldenCookie.MultiplierBonus.ToString()),
                    new("clickPowerBonus", goldenCookie.ClickPowerBonus),
                    new("isRare", goldenCookie.IsRare.ToString())
                });
                await _cache.KeyExpireAsync(cookieKey, goldenCookie.AvailableDuration + TimeSpan.FromMinutes(5));

                await _hubContext.Clients.Group("GameEvents").SendAsync("GoldenCookieSpawned", new
                {
                    goldenCookie.CookieId,
                    goldenCookie.CookieType,
                    goldenCookie.AvailableDuration,
                    goldenCookie.IsRare,
                    goldenCookie.ExpiresAt,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogBusinessEvent(_correlationService, "GoldenCookieSpawned", new
                {
                    goldenCookie.CookieId,
                    goldenCookie.CookieType,
                    goldenCookie.IsRare
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error spawning golden cookie {CookieId}", goldenCookie.CookieId);
            }
        }

        public async Task SpawnGoldenCookieForPlayerAsync(Guid playerId, GoldenCookieNotificationDto goldenCookie)
        {
            try
            {
                await _notificationService.SendNotificationToPlayerAsync(playerId, goldenCookie);

                var cookieKey = $"player_golden_cookie:{playerId}:{goldenCookie.CookieId}";
                await _cache.HashSetAsync(cookieKey, new HashEntry[]
                {
                    new("cookieId", goldenCookie.CookieId),
                    new("playerId", playerId.ToString()),
                    new("type", goldenCookie.CookieType.ToString()),
                    new("expiresAt", goldenCookie.ExpiresAt.ToString("O")),
                    new("multiplier", goldenCookie.MultiplierBonus.ToString())
                });
                await _cache.KeyExpireAsync(cookieKey, goldenCookie.AvailableDuration + TimeSpan.FromMinutes(5));

                _logger.LogBusinessEvent(_correlationService, "PersonalGoldenCookieSpawned", new
                {
                    PlayerId = playerId,
                    goldenCookie.CookieId,
                    goldenCookie.CookieType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error spawning personal golden cookie for player {PlayerId}", playerId);
            }
        }

        public async Task BroadcastSpecialBonusEventAsync(string eventName, Dictionary<string, object> eventData)
        {
            try
            {
                var bonusEvent = new SystemNotificationDto
                {
                    SystemEvent = eventName,
                    EventType = SystemEventType.SpecialEvent,
                    Title = "🌟 Special Bonus Event!",
                    Message = $"A special bonus event '{eventName}' is now active!",
                    Priority = NotificationPriority.High,
                    DisplayDuration = TimeSpan.FromSeconds(8),
                    EventData = eventData
                };

                await _notificationService.BroadcastNotificationAsync(bonusEvent);

                await _hubContext.Clients.Group("GameEvents").SendAsync("SpecialBonusEvent", new
                {
                    EventName = eventName,
                    EventData = eventData,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogBusinessEvent(_correlationService, "SpecialBonusEventBroadcasted", new
                {
                    EventName = eventName,
                    EventDataKeys = eventData.Keys.ToArray()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting special bonus event {EventName}", eventName);
            }
        }

        #endregion

        #region System Status

        public async Task BroadcastSystemStatusAsync(string status, Dictionary<string, object> statusData)
        {
            try
            {
                var statusNotification = new SystemNotificationDto
                {
                    SystemEvent = "system_status",
                    EventType = SystemEventType.Announcement,
                    Title = "System Status Update",
                    Message = status,
                    EventData = statusData,
                    Priority = NotificationPriority.Normal,
                    DisplayDuration = TimeSpan.FromSeconds(5)
                };

                await _notificationService.BroadcastNotificationAsync(statusNotification);

                await _hubContext.Clients.Group("GameEvents").SendAsync("SystemStatusUpdate", new
                {
                    Status = status,
                    StatusData = statusData,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting system status: {Status}", status);
            }
        }

        public async Task BroadcastEmergencyNotificationAsync(string message, string actionRequired)
        {
            try
            {
                var emergency = new SystemNotificationDto
                {
                    SystemEvent = "emergency",
                    EventType = SystemEventType.Emergency,
                    Severity = SystemEventSeverity.Critical,
                    Title = "🚨 Emergency Notification",
                    Message = message,
                    Priority = NotificationPriority.Critical,
                    DisplayDuration = TimeSpan.FromMinutes(2),
                    RequiresAcknowledgment = true,
                    AvailableActions = new List<SystemEventAction>
                    {
                        new() { ActionId = "acknowledge", ActionText = "I Understand", IsPrimary = true },
                        new() { ActionId = "more_info", ActionText = "More Information", ActionUrl = "/emergency-info" }
                    }
                };

                await _notificationService.BroadcastNotificationAsync(emergency);

                _logger.LogBusinessEvent(_correlationService, "EmergencyNotificationSent", new
                {
                    Message = message,
                    ActionRequired = actionRequired
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting emergency notification");
            }
        }

        #endregion

        #region Event Management

        public async Task<IEnumerable<SystemNotificationDto>> GetActiveSystemEventsAsync()
        {
            try
            {
                var activeEventsKey = "active_system_events";
                var eventIds = await _cache.SetMembersAsync(activeEventsKey);

                var events = new List<SystemNotificationDto>();
                foreach (var eventId in eventIds)
                {
                    var eventKey = $"system_event:{eventId}";
                    var eventData = await _cache.StringGetAsync(eventKey);

                    if (eventData.HasValue)
                    {
                        var systemEvent = JsonSerializer.Deserialize<SystemNotificationDto>(eventData!);
                        if (systemEvent != null)
                        {
                            events.Add(systemEvent);
                        }
                    }
                }

                return events.OrderByDescending(e => e.Timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active system events");
                return Enumerable.Empty<SystemNotificationDto>();
            }
        }

        public async Task<IEnumerable<SystemNotificationDto>> GetScheduledEventsAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var events = new List<SystemNotificationDto>();
                // Implementation for getting scheduled events would go here
                // This is a simplified version for demonstration
                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scheduled events from {FromDate} to {ToDate}", fromDate, toDate);
                return Enumerable.Empty<SystemNotificationDto>();
            }
        }

        public async Task AcknowledgeSystemEventAsync(Guid playerId, string eventId)
        {
            try
            {
                var ackKey = $"event_acknowledgments:{eventId}";
                await _cache.SetAddAsync(ackKey, playerId.ToString());
                await _cache.KeyExpireAsync(ackKey, TimeSpan.FromDays(30));

                _logger.LogInformation("Player {PlayerId} acknowledged system event {EventId}", playerId, eventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acknowledging system event {EventId} for player {PlayerId}", eventId, playerId);
            }
        }

        public async Task<bool> HasPlayerAcknowledgedEventAsync(Guid playerId, string eventId)
        {
            try
            {
                var ackKey = $"event_acknowledgments:{eventId}";
                return await _cache.SetContainsAsync(ackKey, playerId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking acknowledgment for event {EventId} and player {PlayerId}", eventId, playerId);
                return false;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task StoreSystemEventAsync(SystemNotificationDto systemEvent)
        {
            try
            {
                var eventKey = $"system_event:{systemEvent.Id}";
                var eventData = JsonSerializer.Serialize(systemEvent);

                await _cache.StringSetAsync(eventKey, eventData, TimeSpan.FromDays(7));
                await _cache.SetAddAsync("active_system_events", systemEvent.Id);

                if (systemEvent.ExpiresAt.HasValue)
                {
                    await _cache.KeyExpireAsync(eventKey, systemEvent.ExpiresAt.Value - DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing system event {EventId}", systemEvent.Id);
            }
        }

        private static string GetSeverityTitle(SystemEventSeverity severity)
        {
            return severity switch
            {
                SystemEventSeverity.Info => "ℹ️ Information",
                SystemEventSeverity.Warning => "⚠️ Warning",
                SystemEventSeverity.Error => "❌ Error",
                SystemEventSeverity.Critical => "🚨 Critical Alert",
                _ => "📢 Announcement"
            };
        }

        private static NotificationPriority GetPriorityFromSeverity(SystemEventSeverity severity)
        {
            return severity switch
            {
                SystemEventSeverity.Critical => NotificationPriority.Critical,
                SystemEventSeverity.Error => NotificationPriority.High,
                SystemEventSeverity.Warning => NotificationPriority.High,
                SystemEventSeverity.Info => NotificationPriority.Normal,
                _ => NotificationPriority.Normal
            };
        }

        private static TimeSpan GetDisplayDurationFromSeverity(SystemEventSeverity severity)
        {
            return severity switch
            {
                SystemEventSeverity.Critical => TimeSpan.FromMinutes(2),
                SystemEventSeverity.Error => TimeSpan.FromSeconds(15),
                SystemEventSeverity.Warning => TimeSpan.FromSeconds(10),
                SystemEventSeverity.Info => TimeSpan.FromSeconds(5),
                _ => TimeSpan.FromSeconds(5)
            };
        }

        private static string GetMaintenancePhaseMessage(MaintenancePhase phase)
        {
            return phase switch
            {
                MaintenancePhase.Scheduled => "Maintenance has been scheduled. We'll notify you before it begins.",
                MaintenancePhase.Warning => "Maintenance will begin soon. Please save your progress.",
                MaintenancePhase.Imminent => "Maintenance starting in a few minutes. Save your progress now!",
                MaintenancePhase.InProgress => "Maintenance is currently in progress. Some features may be unavailable.",
                MaintenancePhase.Completed => "Maintenance has been completed successfully. Thank you for your patience!",
                MaintenancePhase.Extended => "Maintenance has been extended. We apologize for the inconvenience.",
                _ => "Maintenance status update."
            };
        }

        #endregion
    }
}