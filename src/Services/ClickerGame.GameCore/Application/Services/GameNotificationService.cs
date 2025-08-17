using Microsoft.AspNetCore.SignalR;
using ClickerGame.GameCore.Hubs;
using ClickerGame.Shared.Logging;
using ClickerGame.GameCore.Application.DTOs.Notifications;
using ClickerGame.GameCore.Domain.Enums;
using StackExchange.Redis;
using System.Text.Json;

namespace ClickerGame.GameCore.Application.Services
{
    public class GameNotificationService : IGameNotificationService
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILogger<GameNotificationService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly ISignalRConnectionManager _connectionManager;
        private readonly IDatabase _cache;

        public GameNotificationService(
            IHubContext<GameHub> hubContext,
            ILogger<GameNotificationService> logger,
            ICorrelationService correlationService,
            ISignalRConnectionManager connectionManager,
            IConnectionMultiplexer redis)
        {
            _hubContext = hubContext;
            _logger = logger;
            _correlationService = correlationService;
            _connectionManager = connectionManager;
            _cache = redis.GetDatabase();
        }

        #region Generic Notification Methods

        public async Task SendNotificationAsync<T>(T notification) where T : BaseNotificationDto
        {
            try
            {
                switch (notification.TargetType)
                {
                    case NotificationTargetType.Individual:
                        if (notification.TargetPlayerId.HasValue)
                        {
                            await SendNotificationToPlayerAsync(notification.TargetPlayerId.Value, notification);
                        }
                        break;
                    case NotificationTargetType.Broadcast:
                        await BroadcastNotificationAsync(notification);
                        break;
                    case NotificationTargetType.Group:
                        // Implementation for group notifications
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification {NotificationType} with ID {NotificationId}",
                    notification.Type, notification.Id);
            }
        }

        public async Task SendNotificationToPlayerAsync<T>(Guid playerId, T notification) where T : BaseNotificationDto
        {
            try
            {
                var isOnline = await _connectionManager.IsPlayerOnlineAsync(playerId);

                if (isOnline)
                {
                    await _hubContext.Clients.Group($"Player_{playerId}")
                        .SendAsync("NotificationReceived", new
                        {
                            notification.Id,
                            notification.Type,
                            notification.Priority,
                            notification.Title,
                            notification.Message,
                            notification.Timestamp,
                            notification.DisplayDuration,
                            notification.RequiresUserAction,
                            notification.Metadata,
                            Data = notification
                        });

                    _logger.LogInformation("Notification {Type} sent to online player {PlayerId}",
                        notification.Type, playerId);
                }
                else
                {
                    // Store notification for offline player
                    await StoreOfflineNotificationAsync(playerId, notification);
                    _logger.LogInformation("Notification {Type} stored for offline player {PlayerId}",
                        notification.Type, playerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to player {PlayerId}", playerId);
            }
        }

        public async Task SendNotificationToGroupAsync<T>(string groupName, T notification) where T : BaseNotificationDto
        {
            try
            {
                await _hubContext.Clients.Group(groupName)
                    .SendAsync("NotificationReceived", new
                    {
                        notification.Id,
                        notification.Type,
                        notification.Priority,
                        notification.Title,
                        notification.Message,
                        notification.Timestamp,
                        notification.DisplayDuration,
                        notification.RequiresUserAction,
                        notification.Metadata,
                        Data = notification
                    });

                _logger.LogInformation("Notification {Type} sent to group {GroupName}",
                    notification.Type, groupName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to group {GroupName}", groupName);
            }
        }

        public async Task BroadcastNotificationAsync<T>(T notification) where T : BaseNotificationDto
        {
            try
            {
                await _hubContext.Clients.Group("GameEvents")
                    .SendAsync("NotificationReceived", new
                    {
                        notification.Id,
                        notification.Type,
                        notification.Priority,
                        notification.Title,
                        notification.Message,
                        notification.Timestamp,
                        notification.DisplayDuration,
                        notification.RequiresUserAction,
                        notification.Metadata,
                        Data = notification
                    });

                _logger.LogInformation("Notification {Type} broadcasted to all players", notification.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification {Type}", notification.Type);
            }
        }

        #endregion

        #region Specific Notification Methods (Enhanced)

        public async Task SendScoreUpdateNotificationAsync(ScoreNotificationDto notification)
        {
            await SendNotificationToPlayerAsync(notification.PlayerId, notification);

            _logger.LogBusinessEvent(_correlationService, "ScoreUpdateNotificationSent", new
            {
                notification.PlayerId,
                notification.CurrentScore,
                notification.EarnedAmount
            });
        }

        public async Task SendAchievementNotificationAsync(AchievementNotificationDto notification)
        {
            try
            {
                // Store in achievement history first
                await StoreAchievementInHistoryAsync(notification);

                // Send the notification
                await SendNotificationToPlayerAsync(notification.PlayerId, notification);

                // Send celebration if this is a new unlock
                if (notification.IsFirstTime)
                {
                    await SendAchievementUnlockedWithCelebrationAsync(notification);
                }

                _logger.LogBusinessEvent(_correlationService, "AchievementNotificationSent", new
                {
                    notification.PlayerId,
                    notification.AchievementId,
                    notification.AchievementName,
                    notification.Rarity,
                    notification.Points,
                    IsFirstTime = notification.IsFirstTime
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending achievement notification to player {PlayerId}", notification.PlayerId);
            }
        }

        public async Task SendUpgradeNotificationAsync(UpgradeNotificationDto notification)
        {
            await SendNotificationToPlayerAsync(notification.PlayerId, notification);

            _logger.LogBusinessEvent(_correlationService, "UpgradeNotificationSent", new
            {
                notification.PlayerId,
                notification.UpgradeName,
                notification.NewLevel
            });
        }

        public async Task SendSystemNotificationAsync(SystemNotificationDto notification)
        {
            await BroadcastNotificationAsync(notification);

            _logger.LogInformation("System notification sent: {Message}", notification.Message);
        }

        public async Task SendPresenceNotificationAsync(PresenceNotificationDto notification)
        {
            await BroadcastNotificationAsync(notification);
        }

        public async Task SendEventNotificationAsync(EventNotificationDto notification)
        {
            await BroadcastNotificationAsync(notification);

            _logger.LogBusinessEvent(_correlationService, "EventNotificationSent", new
            {
                notification.EventName,
                notification.EventType
            });
        }

        public async Task SendErrorNotificationAsync(ErrorNotificationDto notification)
        {
            if (notification.TargetPlayerId.HasValue)
            {
                await SendNotificationToPlayerAsync(notification.TargetPlayerId.Value, notification);
            }
            else
            {
                await BroadcastNotificationAsync(notification);
            }
        }

        #endregion

        #region Backward Compatibility Methods

        public async Task SendScoreUpdateAsync(Guid playerId, string score, long clickCount, string clickPower, decimal passiveIncome)
        {
            var notification = new ScoreNotificationDto
            {
                PlayerId = playerId,
                CurrentScore = score,
                ClickCount = clickCount,
                ClickPower = clickPower,
                PassiveIncome = passiveIncome,
                Title = "Score Updated",
                Message = $"Your score is now {score}",
                TargetPlayerId = playerId
            };

            await SendScoreUpdateNotificationAsync(notification);
        }

        public async Task SendAchievementNotificationAsync(Guid playerId, string achievementId, string title, string description)
        {
            var notification = new AchievementNotificationDto
            {
                PlayerId = playerId,
                AchievementId = achievementId,
                AchievementName = title,
                Description = description,
                Title = "Achievement Unlocked!",
                Message = $"You unlocked: {title}",
                TargetPlayerId = playerId
            };

            await SendAchievementNotificationAsync(notification);
        }

        public async Task SendUpgradePurchaseNotificationAsync(Guid playerId, string upgradeName, int newLevel, string totalCost)
        {
            var notification = new UpgradeNotificationDto
            {
                PlayerId = playerId,
                UpgradeName = upgradeName,
                NewLevel = newLevel,
                PreviousLevel = newLevel - 1,
                TotalCost = totalCost,
                Title = "Upgrade Purchased",
                Message = $"{upgradeName} upgraded to level {newLevel}",
                TargetPlayerId = playerId
            };

            await SendUpgradeNotificationAsync(notification);
        }

        public async Task BroadcastSystemMessageAsync(string message, string messageType = "info")
        {
            var notification = new SystemNotificationDto
            {
                Title = "System Message",
                Message = message,
                SystemEvent = messageType,
                Priority = messageType.ToLower() switch
                {
                    "error" => NotificationPriority.High,
                    "warning" => NotificationPriority.High,
                    "critical" => NotificationPriority.Critical,
                    _ => NotificationPriority.Normal
                }
            };

            await SendSystemNotificationAsync(notification);
        }

        public async Task BroadcastEventNotificationAsync(string eventName, object eventData)
        {
            var notification = new EventNotificationDto
            {
                EventName = eventName,
                Title = $"Game Event: {eventName}",
                Message = $"A new event has started: {eventName}",
                EventData = eventData as Dictionary<string, object> ?? new Dictionary<string, object>()
            };

            await SendEventNotificationAsync(notification);
        }

        public async Task SendPlayerPresenceUpdateAsync(Guid playerId, bool isOnline)
        {
            // This method can be enhanced with more presence information
            var connections = await _connectionManager.GetPlayerConnectionsAsync(playerId);
            var username = connections.FirstOrDefault()?.Username ?? "Unknown";
            var onlineCount = await _connectionManager.GetOnlinePlayerCountAsync();

            var notification = new PresenceNotificationDto
            {
                PlayerId = playerId,
                Username = username,
                IsOnline = isOnline,
                Status = isOnline ? "online" : "offline",
                OnlinePlayerCount = onlineCount,
                Title = "Player Status",
                Message = $"{username} is now {(isOnline ? "online" : "offline")}"
            };

            await SendPresenceNotificationAsync(notification);
        }

        public async Task SendOnlinePlayersCountAsync()
        {
            var count = await _connectionManager.GetOnlinePlayerCountAsync();

            await _hubContext.Clients.Group("GameEvents").SendAsync("OnlinePlayersCount", new
            {
                Count = count,
                Timestamp = DateTime.UtcNow
            });
        }

        #endregion

        #region Notification Management

        public async Task<bool> IsPlayerOnlineAsync(Guid playerId)
        {
            return await _connectionManager.IsPlayerOnlineAsync(playerId);
        }

        public async Task<int> GetOnlinePlayersCountAsync()
        {
            return await _connectionManager.GetOnlinePlayerCountAsync();
        }

        public async Task MarkNotificationAsReadAsync(Guid playerId, string notificationId)
        {
            try
            {
                var key = $"player_notifications_read:{playerId}";
                await _cache.SetAddAsync(key, notificationId);
                await _cache.KeyExpireAsync(key, TimeSpan.FromDays(30));

                _logger.LogDebug("Notification {NotificationId} marked as read for player {PlayerId}",
                    notificationId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read for player {PlayerId}", playerId);
            }
        }

        public async Task ClearPlayerNotificationsAsync(Guid playerId)
        {
            try
            {
                var offlineKey = $"player_notifications:{playerId}";
                var readKey = $"player_notifications_read:{playerId}";

                await _cache.KeyDeleteAsync(offlineKey);
                await _cache.KeyDeleteAsync(readKey);

                _logger.LogInformation("Cleared all notifications for player {PlayerId}", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing notifications for player {PlayerId}", playerId);
            }
        }

        public async Task<IEnumerable<BaseNotificationDto>> GetUnreadNotificationsAsync(Guid playerId)
        {
            try
            {
                var offlineKey = $"player_notifications:{playerId}";
                var readKey = $"player_notifications_read:{playerId}";

                var allNotifications = await _cache.ListRangeAsync(offlineKey);
                var readNotifications = await _cache.SetMembersAsync(readKey);
                var readIds = readNotifications.Select(r => r.ToString()).ToHashSet();

                var unreadNotifications = new List<BaseNotificationDto>();

                foreach (var notification in allNotifications)
                {
                    if (notification.HasValue)
                    {
                        try
                        {
                            var notificationObj = JsonSerializer.Deserialize<BaseNotificationDto>(notification!);
                            if (notificationObj != null && !readIds.Contains(notificationObj.Id))
                            {
                                unreadNotifications.Add(notificationObj);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize notification for player {PlayerId}", playerId);
                        }
                    }
                }

                return unreadNotifications.OrderByDescending(n => n.Timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications for player {PlayerId}", playerId);
                return Enumerable.Empty<BaseNotificationDto>();
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task StoreOfflineNotificationAsync<T>(Guid playerId, T notification) where T : BaseNotificationDto
        {
            try
            {
                var key = $"player_notifications:{playerId}";
                var serialized = JsonSerializer.Serialize(notification);

                // Store in a list with expiration
                await _cache.ListLeftPushAsync(key, serialized);
                await _cache.KeyExpireAsync(key, TimeSpan.FromDays(7));

                // Keep only the last 100 notifications per player
                await _cache.ListTrimAsync(key, 0, 99);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing offline notification for player {PlayerId}", playerId);
            }
        }

        #endregion

        #region Achievement-Specific Notification Methods

        public async Task SendAchievementProgressUpdateAsync(AchievementProgressNotificationDto notification)
        {
            try
            {
                // Only send if it's a significant milestone or near completion
                if (notification.IsNearCompletion || !string.IsNullOrEmpty(notification.MilestoneReached))
                {
                    await SendNotificationToPlayerAsync(notification.PlayerId, notification);

                    _logger.LogBusinessEvent(_correlationService, "AchievementProgressUpdateSent", new
                    {
                        notification.PlayerId,
                        notification.AchievementId,
                        notification.ProgressPercentage,
                        notification.IsNearCompletion
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending achievement progress update to player {PlayerId}", notification.PlayerId);
            }
        }

        public async Task SendAchievementUnlockedWithCelebrationAsync(AchievementNotificationDto notification)
        {
            try
            {
                // Send the main achievement notification
                await SendNotificationToPlayerAsync(notification.PlayerId, notification);

                // Send celebration animation
                await _hubContext.Clients.Group($"Player_{notification.PlayerId}")
                    .SendAsync("PlayAchievementCelebration", new
                    {
                        notification.AchievementId,
                        notification.Rarity,
                        Animation = notification.CelebrationAnimation ?? GetDefaultCelebrationAnimation(notification.Rarity),
                        SoundEffect = notification.SoundEffect ?? GetDefaultSoundEffect(notification.Rarity),
                        Duration = GetCelebrationDuration(notification.Rarity),
                        ShowConfetti = notification.Rarity == "legendary" || notification.Rarity == "epic",
                        Timestamp = DateTime.UtcNow
                    });

                // Global broadcast for rare achievements
                if (notification.ShowGlobalBroadcast && (notification.Rarity == "legendary" || notification.Rarity == "epic"))
                {
                    await BroadcastRareAchievementAsync(notification);
                }

                _logger.LogBusinessEvent(_correlationService, "AchievementUnlockedWithCelebration", new
                {
                    notification.PlayerId,
                    notification.AchievementId,
                    notification.Rarity,
                    GlobalBroadcast = notification.ShowGlobalBroadcast
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending achievement unlock celebration to player {PlayerId}", notification.PlayerId);
            }
        }

        public async Task SendAchievementSeriesCompletedAsync(Guid playerId, string seriesName, List<string> completedAchievements, AchievementReward? seriesReward = null)
        {
            try
            {
                var notification = new AchievementNotificationDto
                {
                    PlayerId = playerId,
                    AchievementId = $"series_{seriesName.ToLower().Replace(" ", "_")}",
                    AchievementName = $"{seriesName} Series Completed",
                    Description = $"Completed all {completedAchievements.Count} achievements in the {seriesName} series!",
                    Category = "series",
                    Rarity = "epic",
                    Points = completedAchievements.Count * 10,
                    Title = "🏆 Achievement Series Completed!",
                    Message = $"Congratulations! You've mastered the {seriesName} series!",
                    TargetPlayerId = playerId,
                    Priority = NotificationPriority.High,
                    DisplayDuration = TimeSpan.FromSeconds(12),
                    CelebrationAnimation = "series_completion",
                    SoundEffect = "series_fanfare.mp3",
                    Rewards = seriesReward != null ? new List<AchievementReward> { seriesReward } : new(),
                    Metadata = new Dictionary<string, object>
                    {
                        ["seriesName"] = seriesName,
                        ["completedAchievements"] = completedAchievements,
                        ["isSeriesCompletion"] = true
                    }
                };

                await SendAchievementUnlockedWithCelebrationAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending achievement series completion to player {PlayerId}", playerId);
            }
        }

        public async Task SendDailyAchievementSummaryAsync(Guid playerId, List<AchievementNotificationDto> dailyAchievements)
        {
            try
            {
                if (!dailyAchievements.Any()) return;

                var totalPoints = dailyAchievements.Sum(a => a.Points);
                var notification = new SystemNotificationDto
                {
                    Title = "🗓️ Daily Achievement Summary",
                    Message = $"Today you unlocked {dailyAchievements.Count} achievements and earned {totalPoints} points!",
                    SystemEvent = "daily_summary",
                    Priority = NotificationPriority.Normal,
                    TargetPlayerId = playerId,
                    TargetType = NotificationTargetType.Individual,
                    Metadata = new Dictionary<string, object>
                    {
                        ["dailyAchievements"] = dailyAchievements.Select(a => new
                        {
                            a.AchievementId,
                            a.AchievementName,
                            a.Points,
                            a.Rarity
                        }).ToList(),
                        ["totalPoints"] = totalPoints,
                        ["achievementCount"] = dailyAchievements.Count
                    }
                };

                await SendNotificationToPlayerAsync(playerId, notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending daily achievement summary to player {PlayerId}", playerId);
            }
        }

        private async Task BroadcastRareAchievementAsync(AchievementNotificationDto originalNotification)
        {
            try
            {
                // Get player username for broadcast
                var connections = await _connectionManager.GetPlayerConnectionsAsync(originalNotification.PlayerId);
                var username = connections.FirstOrDefault()?.Username ?? "Unknown Player";

                var broadcastNotification = new SystemNotificationDto
                {
                    Title = $"🌟 {originalNotification.Rarity.ToUpper()} Achievement Alert!",
                    Message = $"{username} just unlocked the {originalNotification.Rarity} achievement '{originalNotification.AchievementName}'!",
                    SystemEvent = "rare_achievement_broadcast",
                    Priority = NotificationPriority.High,
                    TargetType = NotificationTargetType.Broadcast,
                    DisplayDuration = TimeSpan.FromSeconds(8),
                    Metadata = new Dictionary<string, object>
                    {
                        ["achieverPlayerId"] = originalNotification.PlayerId,
                        ["achieverUsername"] = username,
                        ["achievementId"] = originalNotification.AchievementId,
                        ["achievementName"] = originalNotification.AchievementName,
                        ["rarity"] = originalNotification.Rarity,
                        ["points"] = originalNotification.Points,
                        ["isGlobalBroadcast"] = true
                    }
                };

                await BroadcastNotificationAsync(broadcastNotification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting rare achievement for player {PlayerId}", originalNotification.PlayerId);
            }
        }

        private static string GetDefaultCelebrationAnimation(string rarity)
        {
            return rarity.ToLower() switch
            {
                "legendary" => "legendary_explosion",
                "epic" => "epic_burst",
                "rare" => "rare_sparkle",
                "uncommon" => "uncommon_glow",
                _ => "common_flash"
            };
        }

        private static string GetDefaultSoundEffect(string rarity)
        {
            return rarity.ToLower() switch
            {
                "legendary" => "legendary_fanfare.mp3",
                "epic" => "epic_chime.mp3",
                "rare" => "rare_bell.mp3",
                "uncommon" => "uncommon_ding.mp3",
                _ => "common_ping.mp3"
            };
        }

        private static int GetCelebrationDuration(string rarity)
        {
            return rarity.ToLower() switch
            {
                "legendary" => 5000,
                "epic" => 4000,
                "rare" => 3000,
                "uncommon" => 2000,
                _ => 1500
            };
        }

        #endregion

        #region Achievement History and Queue Management

        public async Task<IEnumerable<AchievementNotificationDto>> GetPlayerAchievementHistoryAsync(Guid playerId, int pageSize = 50, int pageNumber = 1)
        {
            try
            {
                var key = $"player_achievement_history:{playerId}";
                var start = (pageNumber - 1) * pageSize;
                var stop = start + pageSize - 1;

                var achievementData = await _cache.ListRangeAsync(key, start, stop);
                var achievements = new List<AchievementNotificationDto>();

                foreach (var data in achievementData)
                {
                    if (data.HasValue)
                    {
                        try
                        {
                            var achievement = JsonSerializer.Deserialize<AchievementNotificationDto>(data!);
                            if (achievement != null)
                            {
                                achievements.Add(achievement);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize achievement history for player {PlayerId}", playerId);
                        }
                    }
                }

                return achievements.OrderByDescending(a => a.UnlockedAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting achievement history for player {PlayerId}", playerId);
                return Enumerable.Empty<AchievementNotificationDto>();
            }
        }

        public async Task<int> GetUnreadAchievementCountAsync(Guid playerId)
        {
            try
            {
                var unreadKey = $"player_unread_achievements:{playerId}";
                return (int)await _cache.SetLengthAsync(unreadKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread achievement count for player {PlayerId}", playerId);
                return 0;
            }
        }

        public async Task MarkAchievementAsSeenAsync(Guid playerId, string achievementId)
        {
            try
            {
                var unreadKey = $"player_unread_achievements:{playerId}";
                await _cache.SetRemoveAsync(unreadKey, achievementId);

                _logger.LogDebug("Achievement {AchievementId} marked as seen for player {PlayerId}", achievementId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking achievement as seen for player {PlayerId}", playerId);
            }
        }

        public async Task<IEnumerable<AchievementNotificationDto>> GetRecentAchievementsAsync(Guid playerId, TimeSpan? timeframe = null)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - (timeframe ?? TimeSpan.FromDays(7));
                var allAchievements = await GetPlayerAchievementHistoryAsync(playerId, 100, 1);

                return allAchievements.Where(a => a.UnlockedAt >= cutoffTime)
                                     .OrderByDescending(a => a.UnlockedAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent achievements for player {PlayerId}", playerId);
                return Enumerable.Empty<AchievementNotificationDto>();
            }
        }

        private async Task StoreAchievementInHistoryAsync(AchievementNotificationDto achievement)
        {
            try
            {
                var historyKey = $"player_achievement_history:{achievement.PlayerId}";
                var unreadKey = $"player_unread_achievements:{achievement.PlayerId}";
                var serialized = JsonSerializer.Serialize(achievement);

                // Store in achievement history
                await _cache.ListLeftPushAsync(historyKey, serialized);
                await _cache.KeyExpireAsync(historyKey, TimeSpan.FromDays(365)); // Keep for 1 year

                // Keep only the last 500 achievements per player
                await _cache.ListTrimAsync(historyKey, 0, 499);

                // Add to unread achievements set
                await _cache.SetAddAsync(unreadKey, achievement.AchievementId);
                await _cache.KeyExpireAsync(unreadKey, TimeSpan.FromDays(30));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing achievement in history for player {PlayerId}", achievement.PlayerId);
            }
        }

        public async Task SendMaintenanceNotificationAsync(MaintenanceNotificationDto notification)
        {
            try
            {
                if (notification.TargetType == NotificationTargetType.Broadcast)
                {
                    await _hubContext.Clients.All.SendAsync("MaintenanceNotification", notification);
                }
                else if (notification.TargetPlayerId.HasValue)
                {
                    await _hubContext.Clients.Group($"Player_{notification.TargetPlayerId}")
                        .SendAsync("MaintenanceNotification", notification);
                }

                await StoreNotificationAsync(notification);

                _logger.LogInformation("Maintenance notification sent: {Title}", notification.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending maintenance notification");
            }
        }

        public async Task SendEventCountdownNotificationAsync(EventCountdownNotificationDto notification)
        {
            try
            {
                if (notification.TargetType == NotificationTargetType.Broadcast)
                {
                    await _hubContext.Clients.Group("GameEvents").SendAsync("EventCountdownNotification", notification);
                }
                else if (notification.TargetPlayerId.HasValue)
                {
                    await _hubContext.Clients.Group($"Player_{notification.TargetPlayerId}")
                        .SendAsync("EventCountdownNotification", notification);
                }

                await StoreNotificationAsync(notification);

                _logger.LogInformation("Event countdown notification sent: {EventId}", notification.CountdownEventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending event countdown notification");
            }
        }

        public async Task SendGoldenCookieNotificationAsync(GoldenCookieNotificationDto notification)
        {
            try
            {
                if (notification.TargetType == NotificationTargetType.Broadcast)
                {
                    await _hubContext.Clients.Group("GameEvents").SendAsync("GoldenCookieNotification", notification);
                }
                else if (notification.TargetPlayerId.HasValue)
                {
                    await _hubContext.Clients.Group($"Player_{notification.TargetPlayerId}")
                        .SendAsync("GoldenCookieNotification", notification);
                }

                await StoreNotificationAsync(notification);

                _logger.LogInformation("Golden cookie notification sent: {CookieId}", notification.CookieId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending golden cookie notification");
            }
        }

        public async Task SendAnnouncementNotificationAsync(AnnouncementNotificationDto notification)
        {
            try
            {
                if (notification.TargetType == NotificationTargetType.Broadcast)
                {
                    await _hubContext.Clients.All.SendAsync("AnnouncementNotification", notification);
                }
                else if (notification.TargetPlayerId.HasValue)
                {
                    await _hubContext.Clients.Group($"Player_{notification.TargetPlayerId}")
                        .SendAsync("AnnouncementNotification", notification);
                }

                await StoreNotificationAsync(notification);

                _logger.LogInformation("Announcement notification sent: {AnnouncementId}", notification.AnnouncementId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending announcement notification");
            }
        }


        private async Task StoreNotificationAsync<T>(T notification) where T : BaseNotificationDto
        {
            try
            {
                if (notification.TargetPlayerId.HasValue)
                {
                    await StoreOfflineNotificationAsync(notification.TargetPlayerId.Value, notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing notification for player {PlayerId}", notification.TargetPlayerId);
            }
        }

        #endregion
    }
}