using ClickerGame.GameCore.Application.DTOs;
using ClickerGame.GameCore.Application.DTOs.Notifications;
using ClickerGame.GameCore.Application.Services;
using ClickerGame.GameCore.Domain.Enums;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Authorization;
using ClickerGame.GameCore.Domain.ValueObjects;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Security.Claims;

namespace ClickerGame.GameCore.Hubs
{
    [Authorize(Policy = "RequireValidPlayerId")]
    public class GameHub : Hub
    {
        private readonly IGameEngineService _gameEngine;
        private readonly ILogger<GameHub> _logger;
        private readonly IDatabase _cache;
        private readonly ICorrelationService _correlationService;
        private readonly ISignalRConnectionManager _connectionManager;
        private readonly IGameNotificationService _notificationService;
        private readonly ISystemEventService _systemEventService;
        private readonly IScoreUpdateThrottleService _scoreThrottleService;
        private readonly IPresenceService _presenceService;

        public GameHub(
            IGameEngineService gameEngine,
            ILogger<GameHub> logger,
            ICorrelationService correlationService,
            ISignalRConnectionManager connectionManager,
            IGameNotificationService notificationService,
            IConnectionMultiplexer redis,
            ISystemEventService systemEventService,
            IScoreUpdateThrottleService scoreThrottleService,
             IPresenceService presenceService)
        {
            _gameEngine = gameEngine;
            _logger = logger;
            _cache = redis.GetDatabase();
            _correlationService = correlationService;
            _connectionManager = connectionManager;
            _notificationService = notificationService;
            _systemEventService = systemEventService;
            _scoreThrottleService = scoreThrottleService;
            _presenceService = presenceService;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                var username = GetUsernameFromContext();
                var connectionId = Context.ConnectionId;
                var userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].FirstOrDefault();
                var ipAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();

                _logger.LogInformation("Player {PlayerId} ({Username}) connecting with connection {ConnectionId} from {IpAddress}",
                    playerId, username, connectionId, ipAddress);

                // Validate user authentication
                if (!IsValidUser(playerId, username))
                {
                    _logger.LogWarning("Invalid user authentication for connection {ConnectionId}", connectionId);
                    Context.Abort();
                    return;
                }

                // Add to connection manager and presence service
                await _connectionManager.AddConnectionAsync(connectionId, playerId, username);
                await _presenceService.AddConnectionAsync(connectionId, playerId, username, userAgent, ipAddress);

                // Join player-specific group for targeted messages
                await Groups.AddToGroupAsync(connectionId, $"Player_{playerId}");

                // Join global game events group for broadcasts
                await Groups.AddToGroupAsync(connectionId, "GameEvents");

                // Send initial game state and presence info
                try
                {
                    var session = await _gameEngine.GetGameSessionAsync(playerId);
                    var presence = await _presenceService.GetPlayerPresenceAsync(playerId);
                    var onlineCount = await _presenceService.GetOnlinePlayerCountAsync();

                    await Clients.Caller.SendAsync("InitialGameState", new
                    {
                        PlayerId = playerId,
                        Score = session.Score.ToString(),
                        ClickCount = session.ClickCount,
                        ClickPower = session.ClickPower.ToString(),
                        PassiveIncome = session.PassiveIncomePerSecond,
                        IsActive = session.IsActive,
                        Presence = presence,
                        OnlinePlayersCount = onlineCount,
                        ConnectedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not send initial game state to player {PlayerId}", playerId);
                }

                _logger.LogBusinessEvent(_correlationService, "PlayerConnectedToHub", new
                {
                    PlayerId = playerId,
                    Username = username,
                    ConnectionId = connectionId,
                    UserAgent = userAgent,
                    IpAddress = ipAddress
                });

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync for connection {ConnectionId}", Context.ConnectionId);
                Context.Abort();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                var username = GetUsernameFromContext();
                var connectionId = Context.ConnectionId;

                _logger.LogInformation("Player {PlayerId} ({Username}) disconnecting from connection {ConnectionId}",
                    playerId, username, connectionId);

                // Remove from connection manager and presence service
                await _connectionManager.RemoveConnectionAsync(connectionId);
                await _presenceService.RemoveConnectionAsync(connectionId);

                _logger.LogBusinessEvent(_correlationService, "PlayerDisconnectedFromHub", new
                {
                    PlayerId = playerId,
                    Username = username,
                    ConnectionId = connectionId,
                    Exception = exception?.Message
                });

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync for connection {ConnectionId}", Context.ConnectionId);
            }
        }

        // Enhanced hub methods with validation
        [HubMethodName("RequestScoreUpdate")]
        public async Task RequestScoreUpdate()
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                var session = await _gameEngine.GetGameSessionAsync(playerId);

                await Clients.Caller.SendAsync("ScoreUpdate", new
                {
                    PlayerId = playerId,
                    Score = session.Score.ToString(),
                    ClickCount = session.ClickCount,
                    ClickPower = session.ClickPower.ToString(),
                    PassiveIncome = session.PassiveIncomePerSecond,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (UnauthorizedAccessException)
            {
                await Clients.Caller.SendAsync("Error", new { message = "Unauthorized access" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending score update to player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get score update" });
            }
        }

        [HubMethodName("JoinGroup")]
        public async Task JoinGroup(string groupName)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                // Validate group name (prevent malicious group joining)
                if (!IsValidGroupName(groupName))
                {
                    await Clients.Caller.SendAsync("Error", new { message = "Invalid group name" });
                    return;
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                _logger.LogInformation("Player {PlayerId} joined group {GroupName}", playerId, groupName);

                await Clients.Group(groupName).SendAsync("PlayerJoinedGroup", new
                {
                    PlayerId = playerId,
                    GroupName = groupName,
                    JoinedAt = DateTime.UtcNow
                });
            }
            catch (UnauthorizedAccessException)
            {
                await Clients.Caller.SendAsync("Error", new { message = "Unauthorized access" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining group {GroupName} for player {PlayerId}", groupName, GetPlayerIdFromContext());
            }
        }

        // Helper methods for authentication and validation
        private Guid GetPlayerIdFromContext()
        {
            var playerIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(playerIdClaim, out var playerId))
            {
                return playerId;
            }

            _logger.LogWarning("Invalid or missing player ID in SignalR context for connection {ConnectionId}", Context.ConnectionId);
            throw new UnauthorizedAccessException("Invalid player ID in token");
        }

        private string GetUsernameFromContext()
        {
            return Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        }

        private string GetUserRole()
        {
            return Context.User?.FindFirst("role")?.Value ?? "unknown";
        }

        private bool IsValidUser(Guid playerId, string username)
        {
            // Basic validation
            if (playerId == Guid.Empty || string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            // Check if user has the correct role
            var role = GetUserRole();
            if (role != "player")
            {
                _logger.LogWarning("User {PlayerId} has invalid role: {Role}", playerId, role);
                return false;
            }

            return true;
        }

        private void ValidatePlayerAccess(Guid playerId)
        {
            var contextPlayerId = GetPlayerIdFromContext();
            if (contextPlayerId != playerId)
            {
                throw new UnauthorizedAccessException("Player ID mismatch");
            }
        }

        private static bool IsValidGroupName(string groupName)
        {
            // Implement group name validation logic
            if (string.IsNullOrWhiteSpace(groupName) || groupName.Length > 50)
                return false;

            // Allow only specific group patterns
            var allowedPrefixes = new[] { "Player_", "Guild_", "GameEvents", "Leaderboard_" };
            return allowedPrefixes.Any(prefix => groupName.StartsWith(prefix));
        }

        [HubMethodName("MarkNotificationAsRead")]
        public async Task MarkNotificationAsRead(string notificationId)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                await _notificationService.MarkNotificationAsReadAsync(playerId, notificationId);

                _logger.LogInformation("Player {PlayerId} marked notification {NotificationId} as read",
                    playerId, notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to mark notification as read" });
            }
        }

        [HubMethodName("GetUnreadNotifications")]
        public async Task GetUnreadNotifications()
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                var notifications = await _notificationService.GetUnreadNotificationsAsync(playerId);

                await Clients.Caller.SendAsync("UnreadNotifications", notifications);

                _logger.LogInformation("Sent {Count} unread notifications to player {PlayerId}",
                    notifications.Count(), playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notifications for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get unread notifications" });
            }
        }

        [HubMethodName("ClearAllNotifications")]
        public async Task ClearAllNotifications()
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                await _notificationService.ClearPlayerNotificationsAsync(playerId);

                await Clients.Caller.SendAsync("NotificationsCleared");

                _logger.LogInformation("Cleared all notifications for player {PlayerId}", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing notifications for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to clear notifications" });
            }
        }

        [HubMethodName("SendAchievementUnlocked")]
        public async Task SendAchievementUnlocked(string achievementId, string achievementName, string description, string category = "general", string rarity = "common", int points = 10)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                var notification = new AchievementNotificationDto
                {
                    PlayerId = playerId,
                    AchievementId = achievementId,
                    AchievementName = achievementName,
                    Description = description,
                    Category = category,
                    Rarity = rarity,
                    Points = points,
                    Title = "🏆 Achievement Unlocked!",
                    Message = $"You've earned the '{achievementName}' achievement!",
                    TargetPlayerId = playerId,
                    Metadata = new Dictionary<string, object>
                    {
                        ["celebrationAnimation"] = GetCelebrationAnimation(rarity),
                        ["soundEffect"] = GetAchievementSound(rarity),
                        ["showConfetti"] = rarity == "legendary" || rarity == "epic"
                    }
                };

                await _notificationService.SendAchievementNotificationAsync(notification);

                // Also broadcast to other players for rare achievements
                if (rarity == "legendary" || rarity == "epic")
                {
                    await BroadcastRareAchievementToAll(playerId, GetUsernameFromContext(), achievementName, rarity);
                }

                _logger.LogInformation("Achievement {AchievementId} unlocked for player {PlayerId}", achievementId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending achievement notification for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to send achievement notification" });
            }
        }

        [HubMethodName("GetAchievementProgress")]
        public async Task GetAchievementProgress(string achievementId)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                // This would integrate with future Achievement service
                var progress = await GetAchievementProgressFromService(playerId, achievementId);

                await Clients.Caller.SendAsync("AchievementProgress", new
                {
                    AchievementId = achievementId,
                    PlayerId = playerId,
                    Progress = progress,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("Achievement progress sent for {AchievementId} to player {PlayerId}", achievementId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting achievement progress for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get achievement progress" });
            }
        }

        [HubMethodName("GetAllAchievements")]
        public async Task GetAllAchievements()
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                // This would integrate with future Achievement service
                var achievements = await GetAllAchievementsForPlayer(playerId);

                await Clients.Caller.SendAsync("AllAchievements", new
                {
                    PlayerId = playerId,
                    Achievements = achievements,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("All achievements sent to player {PlayerId}", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all achievements for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get achievements" });
            }
        }

        [HubMethodName("CelebrateAchievement")]
        public async Task CelebrateAchievement(string achievementId)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                // Send celebration animation to the player
                await Clients.Caller.SendAsync("PlayAchievementCelebration", new
                {
                    AchievementId = achievementId,
                    Animation = "fireworks",
                    Duration = 3000,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("Achievement celebration played for {AchievementId} for player {PlayerId}", achievementId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error playing achievement celebration for player {PlayerId}", GetPlayerIdFromContext());
            }
        }

        // Private helper methods
        private async Task BroadcastRareAchievementToAll(Guid playerId, string username, string achievementName, string rarity)
        {
            try
            {
                var broadcastNotification = new AchievementNotificationDto
                {
                    PlayerId = playerId,
                    AchievementName = achievementName,
                    Rarity = rarity,
                    Title = $"🌟 Rare Achievement Alert!",
                    Message = $"{username} just unlocked the {rarity} achievement '{achievementName}'!",
                    TargetType = NotificationTargetType.Broadcast,
                    Priority = NotificationPriority.High,
                    DisplayDuration = TimeSpan.FromSeconds(8),
                    Metadata = new Dictionary<string, object>
                    {
                        ["isGlobalBroadcast"] = true,
                        ["achieverUsername"] = username,
                        ["rarityLevel"] = rarity
                    }
                };

                await _notificationService.BroadcastNotificationAsync(broadcastNotification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting rare achievement for player {PlayerId}", playerId);
            }
        }

        private static string GetCelebrationAnimation(string rarity)
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

        private static string GetAchievementSound(string rarity)
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

        // Placeholder methods for future Achievement service integration
        private async Task<object> GetAchievementProgressFromService(Guid playerId, string achievementId)
        {
            // TODO: Replace with actual Achievement service call
            await Task.Delay(10); // Simulate async call
            return new
            {
                CurrentProgress = 75,
                RequiredProgress = 100,
                IsCompleted = false,
                Description = "Click 1000 times",
                Reward = new { Type = "Score", Amount = "1000" }
            };
        }

        private async Task<object[]> GetAllAchievementsForPlayer(Guid playerId)
        {
            // TODO: Replace with actual Achievement service call
            await Task.Delay(10); // Simulate async call
            return new object[]
            {
        new
        {
            Id = "first_click",
            Name = "First Click",
            Description = "Make your first click",
            Category = "beginner",
            Rarity = "common",
            Points = 5,
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow.AddDays(-1),
            Progress = new { Current = 1, Required = 1 }
        },
        new
        {
            Id = "click_master",
            Name = "Click Master",
            Description = "Make 10,000 clicks",
            Category = "clicking",
            Rarity = "epic",
            Points = 100,
            IsUnlocked = false,
            Progress = new { Current = 2500, Required = 10000 }
        }
            };
        }

        [HubMethodName("AcknowledgeSystemEvent")]
        public async Task AcknowledgeSystemEvent(string eventId)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                // You'll need to inject ISystemEventService into the GameHub constructor
                await _systemEventService.AcknowledgeSystemEventAsync(playerId, eventId);

                await Clients.Caller.SendAsync("SystemEventAcknowledged", new
                {
                    EventId = eventId,
                    PlayerId = playerId,
                    AcknowledgedAt = DateTime.UtcNow
                });

                _logger.LogInformation("Player {PlayerId} acknowledged system event {EventId}", playerId, eventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acknowledging system event for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to acknowledge system event" });
            }
        }

        [HubMethodName("GetActiveSystemEvents")]
        public async Task GetActiveSystemEvents()
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                var activeEvents = await _systemEventService.GetActiveSystemEventsAsync();

                await Clients.Caller.SendAsync("ActiveSystemEvents", new
                {
                    Events = activeEvents,
                    Count = activeEvents.Count(),
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation("Sent {Count} active system events to player {PlayerId}", activeEvents.Count(), playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active system events for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get active system events" });
            }
        }

        [HubMethodName("ClaimGoldenCookie")]
        public async Task ClaimGoldenCookie(string cookieId)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                // Validate golden cookie exists and is not expired
                var cookieKey = $"golden_cookie:{cookieId}";
                var cookieData = await _cache.HashGetAllAsync(cookieKey);

                if (cookieData.Length == 0)
                {
                    await Clients.Caller.SendAsync("Error", new { message = "Golden cookie not found or expired" });
                    return;
                }

                var cookieInfo = cookieData.ToDictionary(x => x.Name!, x => x.Value!);
                var expiresAt = DateTime.Parse(cookieInfo["expiresAt"]);

                if (DateTime.UtcNow > expiresAt)
                {
                    await Clients.Caller.SendAsync("Error", new { message = "Golden cookie has expired" });
                    return;
                }

                // Process golden cookie claim
                var multiplier = decimal.Parse(cookieInfo["multiplier"]);
                var clickPowerBonus = cookieInfo["clickPowerBonus"];
                var isRare = bool.Parse(cookieInfo["isRare"]);

                await Clients.Caller.SendAsync("GoldenCookieClaimed", new
                {
                    CookieId = cookieId,
                    MultiplierBonus = multiplier,
                    ClickPowerBonus = clickPowerBonus,
                    IsRare = isRare,
                    ClaimedAt = DateTime.UtcNow
                });

                // Clean up the golden cookie
                await _cache.KeyDeleteAsync(cookieKey);

                _logger.LogBusinessEvent(_correlationService, "GoldenCookieClaimed", new
                {
                    PlayerId = playerId,
                    CookieId = cookieId,
                    MultiplierBonus = multiplier,
                    IsRare = isRare
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error claiming golden cookie {CookieId} for player {PlayerId}", cookieId, GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to claim golden cookie" });
            }
        }

        [HubMethodName("RequestEventCountdownUpdate")]
        public async Task RequestEventCountdownUpdate(string eventId)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                var countdownKey = $"event_countdown:{eventId}";
                var eventData = await _cache.HashGetAllAsync(countdownKey);

                if (eventData.Length > 0)
                {
                    var eventInfo = eventData.ToDictionary(x => x.Name!, x => x.Value!);
                    var startTime = DateTime.Parse(eventInfo["startTime"]);
                    var endTime = DateTime.Parse(eventInfo["endTime"]);
                    var timeRemaining = startTime - DateTime.UtcNow;

                    if (timeRemaining.TotalSeconds < 0)
                    {
                        timeRemaining = endTime - DateTime.UtcNow;
                    }

                    await Clients.Caller.SendAsync("EventCountdownUpdate", new
                    {
                        EventId = eventId,
                        StartTime = startTime,
                        EndTime = endTime,
                        TimeRemaining = timeRemaining.ToString(),
                        Description = eventInfo.GetValueOrDefault("description", ""),
                        IsActive = bool.Parse(eventInfo.GetValueOrDefault("isActive", "false")),
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", new { message = "Event countdown not found" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting event countdown for {EventId}", eventId);
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get event countdown" });
            }
        }


        [HubMethodName("RequestLiveScoreUpdate")]
        public async Task RequestLiveScoreUpdate()
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                // Check throttling
                if (!await _scoreThrottleService.CanSendScoreUpdateAsync(playerId))
                {
                    var throttleInfo = await _scoreThrottleService.GetThrottleInfoAsync(playerId);
                    await Clients.Caller.SendAsync("ScoreUpdateThrottled", new
                    {
                        PlayerId = playerId,
                        RemainingTime = throttleInfo.RemainingThrottleTime.TotalMilliseconds,
                        Message = "Score updates are rate limited. Please wait.",
                        Timestamp = DateTime.UtcNow
                    });
                    return;
                }

                var session = await _gameEngine.GetGameSessionAsync(playerId);

                var scoreUpdate = new LiveScoreUpdateDto
                {
                    PlayerId = playerId,
                    Score = session.Score.ToString(),
                    Delta = "+0", // No change since this is just a request
                    ClickCount = session.ClickCount,
                    ClickPower = session.ClickPower.ToString(),
                    PassiveIncome = session.PassiveIncomePerSecond,
                    Source = ScoreUpdateSource.Click,
                    ShowAnimation = false // No animation for manual requests
                };

                await Clients.Caller.SendAsync("LiveScoreUpdate", scoreUpdate);
                await _scoreThrottleService.RecordScoreUpdateAsync(playerId);

                _logger.LogInformation("Live score update sent to player {PlayerId}", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending live score update for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get live score update" });
            }
        }

        [HubMethodName("SubscribeToScoreUpdates")]
        public async Task SubscribeToScoreUpdates(bool subscribe = true)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                var groupName = $"ScoreUpdates_{playerId}";

                if (subscribe)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                    _logger.LogInformation("Player {PlayerId} subscribed to score updates", playerId);
                }
                else
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                    _logger.LogInformation("Player {PlayerId} unsubscribed from score updates", playerId);
                }

                await Clients.Caller.SendAsync("ScoreUpdateSubscriptionChanged", new
                {
                    PlayerId = playerId,
                    Subscribed = subscribe,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing score update subscription for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to manage score update subscription" });
            }
        }

        [HubMethodName("GetScoreHistory")]
        public async Task GetScoreHistory(int minutes = 30)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                // Limit the time range to prevent abuse
                minutes = Math.Min(minutes, 1440); // Max 24 hours

                var historyKey = $"score_history:{playerId}";
                var historyData = await _cache.ListRangeAsync(historyKey, 0, -1);

                var snapshots = new List<ScoreSnapshot>();
                var cutoffTime = DateTime.UtcNow.AddMinutes(-minutes);

                foreach (var data in historyData)
                {
                    if (data.HasValue)
                    {
                        try
                        {
                            var snapshot = JsonSerializer.Deserialize<ScoreSnapshot>(data!);
                            if (snapshot != null && snapshot.Timestamp >= cutoffTime)
                            {
                                snapshots.Add(snapshot);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize score snapshot for player {PlayerId}", playerId);
                        }
                    }
                }

                var scoreHistory = new ScoreHistoryDto
                {
                    PlayerId = playerId,
                    ScoreHistory = snapshots.OrderBy(s => s.Timestamp).ToList(),
                    TimeRange = TimeSpan.FromMinutes(minutes)
                };

                await Clients.Caller.SendAsync("ScoreHistory", scoreHistory);

                _logger.LogInformation("Score history sent to player {PlayerId} for {Minutes} minutes", playerId, minutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting score history for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get score history" });
            }
        }

        [HubMethodName("ProcessClick")]
        public async Task ProcessClick(decimal clickPower = 1.0m)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                // Check throttling
                if (!await _scoreThrottleService.CanSendScoreUpdateAsync(playerId))
                {
                    var throttleInfo = await _scoreThrottleService.GetThrottleInfoAsync(playerId);
                    await Clients.Caller.SendAsync("ClickThrottled", new
                    {
                        PlayerId = playerId,
                        RemainingTime = throttleInfo.RemainingThrottleTime.TotalMilliseconds,
                        Message = "Clicking too fast! Please slow down.",
                        Timestamp = DateTime.UtcNow
                    });
                    return;
                }

                // Get current score before processing
                var sessionBefore = await _gameEngine.GetGameSessionAsync(playerId);
                var scoreBefore = sessionBefore.Score;

                // Process the click
                var earnedValue = await _gameEngine.ProcessClickAsync(playerId, new BigNumber(clickPower));
                var sessionAfter = await _gameEngine.GetGameSessionAsync(playerId);

                // Create live score update
                var liveUpdate = new LiveScoreUpdateDto
                {
                    PlayerId = playerId,
                    Score = sessionAfter.Score.ToString(),
                    Delta = $"+{earnedValue}",
                    ClickCount = sessionAfter.ClickCount,
                    ClickPower = sessionAfter.ClickPower.ToString(),
                    PassiveIncome = sessionAfter.PassiveIncomePerSecond,
                    Source = ScoreUpdateSource.Click,
                    ShowAnimation = true,
                    AnimationType = earnedValue > new BigNumber(100) ? "big_click" : "normal_click"
                };

                // Send to player and any subscribers
                await Clients.Group($"ScoreUpdates_{playerId}").SendAsync("LiveScoreUpdate", liveUpdate);
                await Clients.Caller.SendAsync("ClickProcessed", new
                {
                    EarnedValue = earnedValue.ToString(),
                    TotalScore = sessionAfter.Score.ToString(),
                    ClickCount = sessionAfter.ClickCount,
                    Timestamp = DateTime.UtcNow
                });

                // Store score snapshot for history
                await StoreScoreSnapshotAsync(playerId, sessionAfter.Score.ToString(), ScoreUpdateSource.Click);

                // Record throttling
                await _scoreThrottleService.RecordScoreUpdateAsync(playerId);

                _logger.LogBusinessEvent(_correlationService, "ClickProcessedViaHub", new
                {
                    PlayerId = playerId,
                    EarnedValue = earnedValue.ToString(),
                    NewScore = sessionAfter.Score.ToString(),
                    ClickCount = sessionAfter.ClickCount
                });
            }
            catch (InvalidOperationException ex)
            {
                await Clients.Caller.SendAsync("ClickRateLimited", new
                {
                    message = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing click via hub for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to process click" });
            }
        }

        [HubMethodName("GetThrottleInfo")]
        public async Task GetThrottleInfo()
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                var throttleInfo = await _scoreThrottleService.GetThrottleInfoAsync(playerId);

                await Clients.Caller.SendAsync("ThrottleInfo", throttleInfo);

                _logger.LogDebug("Throttle info sent to player {PlayerId}", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting throttle info for player {PlayerId}", GetPlayerIdFromContext());
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get throttle info" });
            }
        }

        // Private helper method for storing score snapshots
        private async Task StoreScoreSnapshotAsync(Guid playerId, string score, ScoreUpdateSource source, string? additionalInfo = null)
        {
            try
            {
                var snapshot = new ScoreSnapshot
                {
                    Score = score,
                    Timestamp = DateTime.UtcNow,
                    Source = source,
                    AdditionalInfo = additionalInfo
                };

                var historyKey = $"score_history:{playerId}";
                var serialized = JsonSerializer.Serialize(snapshot);

                await _cache.ListLeftPushAsync(historyKey, serialized);
                await _cache.KeyExpireAsync(historyKey, TimeSpan.FromDays(1)); // Keep history for 1 day
                await _cache.ListTrimAsync(historyKey, 0, 999); // Keep last 1000 entries
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing score snapshot for player {PlayerId}", playerId);
            }
        }

        [HubMethodName("GetOnlinePlayersCount")]
        public async Task GetOnlinePlayersCount()
        {
            try
            {
                var count = await _presenceService.GetOnlinePlayerCountAsync();
                await Clients.Caller.SendAsync("OnlinePlayersCount", new { Count = count, Timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online players count");
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get online players count" });
            }
        }

        [HubMethodName("GetOnlinePlayers")]
        public async Task GetOnlinePlayers(int limit = 50)
        {
            try
            {
                limit = Math.Min(limit, 100); // Cap at 100
                var onlinePlayers = await _presenceService.GetOnlinePlayersAsync(limit);
                await Clients.Caller.SendAsync("OnlinePlayers", onlinePlayers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online players");
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get online players" });
            }
        }

        [HubMethodName("SetPlayerStatus")]
        public async Task SetPlayerStatus(string status, string? activity = null)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                if (Enum.TryParse<PresenceStatus>(status, true, out var presenceStatus))
                {
                    await _presenceService.SetPlayerStatusAsync(playerId, presenceStatus, activity);

                    await Clients.Caller.SendAsync("StatusUpdated", new
                    {
                        Status = presenceStatus.ToString(),
                        Activity = activity,
                        UpdatedAt = DateTime.UtcNow
                    });

                    _logger.LogInformation("Player {PlayerId} updated status to {Status}", playerId, presenceStatus);
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", new { message = "Invalid status value" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting player status");
                await Clients.Caller.SendAsync("Error", new { message = "Failed to update status" });
            }
        }

        [HubMethodName("SetActivity")]
        public async Task SetActivity(string activity)
        {
            try
            {
                var playerId = GetPlayerIdFromContext();
                ValidatePlayerAccess(playerId);

                await _presenceService.SetPlayerActivityAsync(playerId, activity);
                await Clients.Caller.SendAsync("ActivityUpdated", new { Activity = activity, UpdatedAt = DateTime.UtcNow });

                _logger.LogDebug("Player {PlayerId} updated activity to {Activity}", playerId, activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting player activity");
                await Clients.Caller.SendAsync("Error", new { message = "Failed to update activity" });
            }
        }

        [HubMethodName("GetPlayerPresence")]
        public async Task GetPlayerPresence(string playerIdString)
        {
            try
            {
                if (Guid.TryParse(playerIdString, out var playerId))
                {
                    var presence = await _presenceService.GetPlayerPresenceAsync(playerId);
                    if (presence != null)
                    {
                        await Clients.Caller.SendAsync("PlayerPresence", presence);
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("PlayerPresence", null);
                    }
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", new { message = "Invalid player ID format" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player presence for {PlayerId}", playerIdString);
                await Clients.Caller.SendAsync("Error", new { message = "Failed to get player presence" });
            }
        }

    }
}