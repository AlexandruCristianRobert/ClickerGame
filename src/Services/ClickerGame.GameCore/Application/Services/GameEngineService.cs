using ClickerGame.GameCore.Application.DTOs;
using ClickerGame.GameCore.Application.DTOs.Notifications;
using ClickerGame.GameCore.Application.Services;
using ClickerGame.GameCore.Domain.Entities;
using ClickerGame.GameCore.Domain.Enums;
using ClickerGame.GameCore.Domain.ValueObjects;
using ClickerGame.GameCore.Infrastructure.Data;
using ClickerGame.Shared.Logging;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace ClickerGame.GameCore.Application.Services
{
    public class GameEngineService : IGameEngineService
    {
        private readonly GameCoreDbContext _context;
        private readonly IDatabase _cache;
        private readonly ILogger<GameEngineService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IGameNotificationService _notificationService;
        private readonly IScoreBroadcastService _scoreBroadcastService;
        private readonly ISystemEventService _systemEventService;

        // Rate limiting and anti-cheat
        private readonly TimeSpan _clickRateLimit = TimeSpan.FromMilliseconds(50); // Max 20 clicks per second
        private readonly int _maxClicksPerMinute = 600; // Maximum clicks per minute for anti-cheat

        public GameEngineService(
            GameCoreDbContext context,
            IConnectionMultiplexer redis,
            ILogger<GameEngineService> logger,
            ICorrelationService correlationService,
            IHttpClientFactory httpClientFactory,
            IGameNotificationService notificationService,
            IScoreBroadcastService scoreBroadcastService,
            ISystemEventService systemEventService)
        {
            _context = context;
            _cache = redis.GetDatabase();
            _logger = logger;
            _correlationService = correlationService;
            _httpClientFactory = httpClientFactory;
            _notificationService = notificationService;
            _scoreBroadcastService = scoreBroadcastService;
            _systemEventService = systemEventService;
        }

        public async Task<BigNumber> ProcessClickAsync(Guid playerId, BigNumber clickPower)
        {
            var session = await GetGameSessionFromCacheOrDb(playerId);
            if (session == null)
            {
                throw new InvalidOperationException("Game session not found");
            }

            try
            {
                // Anti-cheat validation
                await ValidateClickPatternAsync(session);

                // Store previous values for comparison and notifications
                var previousScore = session.Score;
                var previousClickCount = session.ClickCount;
                var previousLevel = CalculatePlayerLevel(session.Score);

                // Process the click with enhanced tracking
                var earnedValue = session.ProcessClick(clickPower);
                var newLevel = CalculatePlayerLevel(session.Score);

                // Update cache immediately for responsive UI
                await UpdateSessionInCache(session);

                // Send real-time score update notification
                await SendScoreUpdateNotificationAsync(session, previousScore, earnedValue, ScoreUpdateSource.Click);

                // Check for level up
                if (newLevel > previousLevel)
                {
                    await HandlePlayerLevelUpAsync(session, previousLevel, newLevel);
                }

                // Check for score milestones
                await CheckScoreMilestonesAsync(session, previousScore);

                // Check for click count milestones
                await CheckClickMilestonesAsync(session, previousClickCount);

                // Periodic database saves and notifications
                if (session.ClickCount % 10 == 0 ||
                    DateTime.UtcNow - session.LastUpdateTime > TimeSpan.FromSeconds(30))
                {
                    await SaveGameSessionAsync(session);

                    // Send periodic score snapshot for history
                    await StoreScoreSnapshotAsync(session, ScoreUpdateSource.Click);
                }

                // Check for achievements (integration point for future achievement service)
                await CheckPotentialAchievementsAsync(session, earnedValue, ScoreUpdateSource.Click);

                _logger.LogDebug("Click processed for player {PlayerId}, earned {Value}, new score {NewScore}",
                    playerId, earnedValue, session.Score);

                return earnedValue;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Click rate limit exceeded for player {PlayerId}: {Message}",
                    playerId, ex.Message);
                throw;
            }
        }

        public async Task<BigNumber> ProcessPassiveIncomeAsync(Guid playerId)
        {
            var session = await GetGameSessionFromCacheOrDb(playerId);
            if (session == null)
            {
                return BigNumber.Zero;
            }

            try
            {
                var previousScore = session.Score;
                var timeSinceLastUpdate = DateTime.UtcNow - session.LastUpdateTime;

                // Calculate passive income
                var passiveEarnings = new BigNumber(session.PassiveIncomePerSecond) *
                                     new BigNumber((decimal)timeSinceLastUpdate.TotalSeconds);

                if (passiveEarnings > BigNumber.Zero)
                {
                    session.Score = session.Score + passiveEarnings;
                    session.LastUpdateTime = DateTime.UtcNow;

                    await UpdateSessionInCache(session);

                    // Send passive income notification
                    await SendScoreUpdateNotificationAsync(session, previousScore, passiveEarnings, ScoreUpdateSource.PassiveIncome);

                    // Check for milestones from passive income
                    await CheckScoreMilestonesAsync(session, previousScore);

                    _logger.LogDebug("Passive income processed for player {PlayerId}, earned {Value}",
                        playerId, passiveEarnings);
                }

                return passiveEarnings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing passive income for player {PlayerId}", playerId);
                return BigNumber.Zero;
            }
        }

        public async Task<bool> ApplyUpgradeEffectsAsync(Guid playerId, UpgradeEffectsDto upgradeEffects)
        {
            try
            {
                var session = await GetGameSessionFromCacheOrDb(playerId);
                if (session == null)
                {
                    _logger.LogWarning("Cannot apply upgrade effects - game session not found for player {PlayerId}", playerId);
                    return false;
                }

                var previousClickPower = session.ClickPower;
                var previousPassiveIncome = session.PassiveIncomePerSecond;
                var previousScore = session.Score;

                // Apply upgrade effects
                if (upgradeEffects.ClickPowerBonus > 0)
                {
                    session.ClickPower = session.ClickPower + new BigNumber(upgradeEffects.ClickPowerBonus);
                }

                if (upgradeEffects.PassiveIncomeBonus > 0)
                {
                    session.PassiveIncomePerSecond += upgradeEffects.PassiveIncomeBonus;
                }

                // Apply multipliers (if applicable)
                if (upgradeEffects.MultiplierBonus > 1.0m)
                {
                    session.ClickPower = session.ClickPower * new BigNumber(upgradeEffects.MultiplierBonus);
                    session.PassiveIncomePerSecond *= upgradeEffects.MultiplierBonus;
                }

                session.LastUpdateTime = DateTime.UtcNow;
                await UpdateSessionInCache(session);
                await SaveGameSessionAsync(session);

                // Send upgrade effect notification
                await SendUpgradeEffectNotificationAsync(session, upgradeEffects, previousClickPower, previousPassiveIncome);

                _logger.LogBusinessEvent(_correlationService, "UpgradeEffectsApplied", new
                {
                    PlayerId = playerId,
                    SourceUpgrade = upgradeEffects.SourceUpgradeId,
                    ClickPowerIncrease = upgradeEffects.ClickPowerBonus,
                    PassiveIncomeIncrease = upgradeEffects.PassiveIncomeBonus,
                    NewClickPower = session.ClickPower.ToString(),
                    NewPassiveIncome = session.PassiveIncomePerSecond
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying upgrade effects for player {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<GameSession> GetGameSessionAsync(Guid playerId)
        {
            var session = await GetGameSessionFromCacheOrDb(playerId);
            if (session != null)
            {
                // Calculate and apply offline earnings when player returns
                var offlineEarnings = session.CalculateOfflineEarnings();
                if (offlineEarnings > BigNumber.Zero)
                {
                    var previousScore = session.Score;
                    session.Score = session.Score + offlineEarnings;
                    session.LastUpdateTime = DateTime.UtcNow;

                    await UpdateSessionInCache(session);
                    await SaveGameSessionAsync(session);

                    // Send offline earnings notification
                    await SendScoreUpdateNotificationAsync(session, previousScore, offlineEarnings, ScoreUpdateSource.Offline);

                    _logger.LogBusinessEvent(_correlationService, "OfflineEarningsApplied", new
                    {
                        PlayerId = playerId,
                        OfflineEarnings = offlineEarnings.ToString(),
                        NewScore = session.Score.ToString()
                    });
                }

                // Process any pending passive income
                await ProcessPassiveIncomeAsync(playerId);
            }

            return session ?? throw new InvalidOperationException("Game session not found");
        }

        public async Task<GameSession> CreateGameSessionAsync(Guid playerId, string username)
        {
            var existingSession = await _context.GameSessions
                .FirstOrDefaultAsync(gs => gs.PlayerId == playerId);

            if (existingSession != null)
            {
                existingSession.IsActive = true;
                existingSession.LastUpdateTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await UpdateSessionInCache(existingSession);

                // Send player return notification
                await _notificationService.SendPlayerPresenceUpdateAsync(playerId, true);

                return existingSession;
            }

            var newSession = new GameSession
            {
                SessionId = Guid.NewGuid(),
                PlayerId = playerId,
                PlayerUsername = username,
                Score = BigNumber.Zero,
                ClickCount = 0,
                ClickPower = BigNumber.One,
                PassiveIncomePerSecond = 0,
                StartTime = DateTime.UtcNow,
                LastUpdateTime = DateTime.UtcNow,
                IsActive = true,
                LastAntiCheatCheck = DateTime.UtcNow
            };

            _context.GameSessions.Add(newSession);
            await _context.SaveChangesAsync();
            await UpdateSessionInCache(newSession);

            // Send welcome notification for new players
            await _notificationService.SendSystemNotificationAsync(new SystemNotificationDto
            {
                Title = "Welcome to the Game!",
                Message = $"Welcome {username}! Start clicking to earn your first points!",
                TargetPlayerId = playerId,
                TargetType = NotificationTargetType.Individual,
                Priority = NotificationPriority.Normal
            });

            // Broadcast new player joined
            await _notificationService.SendPlayerPresenceUpdateAsync(playerId, true);

            _logger.LogBusinessEvent(_correlationService, "GameSessionCreated", new
            {
                PlayerId = playerId,
                Username = username,
                SessionId = newSession.SessionId
            });

            return newSession;
        }

        public async Task<BigNumber> CalculateOfflineEarningsAsync(Guid playerId)
        {
            var session = await GetGameSessionAsync(playerId);
            return session.CalculateOfflineEarnings();
        }

        public async Task SaveGameSessionAsync(GameSession session)
        {
            _context.GameSessions.Update(session);
            await _context.SaveChangesAsync();
            await UpdateSessionInCache(session);
        }

        public async Task<bool> ValidatePlayerAsync(Guid playerId, string token)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await httpClient.GetAsync($"http://players-service/api/players/{playerId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate player {PlayerId}", playerId);
                return false;
            }
        }

        #region Private Helper Methods

        private async Task SendScoreUpdateNotificationAsync(GameSession session, BigNumber previousScore,
            BigNumber earnedAmount, ScoreUpdateSource source)
        {
            try
            {
                var scoreNotification = new ScoreNotificationDto
                {
                    PlayerId = session.PlayerId,
                    CurrentScore = session.Score.ToString(),
                    PreviousScore = previousScore.ToString(),
                    EarnedAmount = earnedAmount.ToString(),
                    ClickCount = session.ClickCount,
                    ClickPower = session.ClickPower.ToString(),
                    PassiveIncome = session.PassiveIncomePerSecond,
                    Source = source.ToString().ToLower(),
                    Title = GetScoreUpdateTitle(source),
                    Message = GetScoreUpdateMessage(source, earnedAmount),
                    TargetPlayerId = session.PlayerId
                };

                await _notificationService.SendScoreUpdateNotificationAsync(scoreNotification);

                // Also send live score update for real-time UI
                var liveUpdate = new LiveScoreUpdateDto
                {
                    PlayerId = session.PlayerId,
                    Score = session.Score.ToString(),
                    Delta = $"+{earnedAmount}",
                    ClickCount = session.ClickCount,
                    ClickPower = session.ClickPower.ToString(),
                    PassiveIncome = session.PassiveIncomePerSecond,
                    Source = source,
                    ShowAnimation = ShouldShowAnimation(source, earnedAmount),
                    AnimationType = GetAnimationType(source, earnedAmount)
                };

                await _scoreBroadcastService.BroadcastScoreUpdateAsync(session.PlayerId, new ScoreUpdateDto
                {
                    PlayerId = session.PlayerId,
                    CurrentScore = session.Score.ToString(),
                    PreviousScore = previousScore.ToString(),
                    EarnedAmount = earnedAmount.ToString(),
                    ClickCount = session.ClickCount,
                    ClickPower = session.ClickPower.ToString(),
                    PassiveIncome = session.PassiveIncomePerSecond,
                    Source = source
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending score update notification for player {PlayerId}", session.PlayerId);
            }
        }

        private async Task SendUpgradeEffectNotificationAsync(GameSession session, UpgradeEffectsDto upgradeEffects,
            BigNumber previousClickPower, decimal previousPassiveIncome)
        {
            try
            {
                var upgradeNotification = new UpgradeNotificationDto
                {
                    PlayerId = session.PlayerId,
                    UpgradeId = upgradeEffects.SourceUpgradeId,
                    UpgradeName = "Upgrade Applied", // Would be enhanced with actual upgrade name
                    Title = "🔧 Upgrade Effects Applied!",
                    Message = BuildUpgradeEffectMessage(upgradeEffects, session, previousClickPower, previousPassiveIncome),
                    TargetPlayerId = session.PlayerId,
                    Priority = NotificationPriority.Normal,
                    DisplayDuration = TimeSpan.FromSeconds(5)
                };

                await _notificationService.SendUpgradeNotificationAsync(upgradeNotification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending upgrade effect notification for player {PlayerId}", session.PlayerId);
            }
        }

        private async Task HandlePlayerLevelUpAsync(GameSession session, int previousLevel, int newLevel)
        {
            try
            {
                // Send level up notification
                await _notificationService.SendSystemNotificationAsync(new SystemNotificationDto
                {
                    Title = "🎉 Level Up!",
                    Message = $"Congratulations! You've reached level {newLevel}!",
                    TargetPlayerId = session.PlayerId,
                    TargetType = NotificationTargetType.Individual,
                    Priority = NotificationPriority.High,
                    DisplayDuration = TimeSpan.FromSeconds(8)
                });

                // Potential rewards for leveling up
                var levelUpBonus = CalculateLevelUpBonus(newLevel);
                if (levelUpBonus > BigNumber.Zero)
                {
                    session.Score = session.Score + levelUpBonus;

                    await _notificationService.SendScoreUpdateNotificationAsync(new ScoreNotificationDto
                    {
                        PlayerId = session.PlayerId,
                        CurrentScore = session.Score.ToString(),
                        EarnedAmount = levelUpBonus.ToString(),
                        Source = "level_up",
                        Title = "Level Up Bonus!",
                        Message = $"You received {levelUpBonus} points for reaching level {newLevel}!",
                        TargetPlayerId = session.PlayerId
                    });
                }

                _logger.LogBusinessEvent(_correlationService, "PlayerLevelUp", new
                {
                    PlayerId = session.PlayerId,
                    PreviousLevel = previousLevel,
                    NewLevel = newLevel,
                    LevelUpBonus = levelUpBonus.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling level up for player {PlayerId}", session.PlayerId);
            }
        }

        private async Task CheckScoreMilestonesAsync(GameSession session, BigNumber previousScore)
        {
            try
            {
                var milestones = new[] {
                    new BigNumber(1000), new BigNumber(10000), new BigNumber(100000),
                    new BigNumber(1000000), new BigNumber(10000000), new BigNumber(100000000)
                };

                foreach (var milestone in milestones)
                {
                    if (previousScore < milestone && session.Score >= milestone)
                    {
                        await _scoreBroadcastService.BroadcastMilestoneAchievedAsync(
                            session.PlayerId,
                            $"{milestone} Points",
                            session.Score.ToString());

                        await _notificationService.SendSystemNotificationAsync(new SystemNotificationDto
                        {
                            Title = "🏆 Milestone Achieved!",
                            Message = $"You've reached {milestone} points! Keep clicking!",
                            TargetPlayerId = session.PlayerId,
                            TargetType = NotificationTargetType.Individual,
                            Priority = NotificationPriority.High
                        });

                        _logger.LogBusinessEvent(_correlationService, "ScoreMilestoneReached", new
                        {
                            PlayerId = session.PlayerId,
                            Milestone = milestone.ToString(),
                            CurrentScore = session.Score.ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking score milestones for player {PlayerId}", session.PlayerId);
            }
        }

        private async Task CheckClickMilestonesAsync(GameSession session, long previousClickCount)
        {
            try
            {
                var clickMilestones = new[] { 100L, 1000L, 10000L, 100000L, 1000000L };

                foreach (var milestone in clickMilestones)
                {
                    if (previousClickCount < milestone && session.ClickCount >= milestone)
                    {
                        await _notificationService.SendSystemNotificationAsync(new SystemNotificationDto
                        {
                            Title = "👆 Click Milestone!",
                            Message = $"You've made {milestone:N0} clicks! Your dedication is impressive!",
                            TargetPlayerId = session.PlayerId,
                            TargetType = NotificationTargetType.Individual,
                            Priority = NotificationPriority.Normal
                        });

                        _logger.LogBusinessEvent(_correlationService, "ClickMilestoneReached", new
                        {
                            PlayerId = session.PlayerId,
                            Milestone = milestone,
                            TotalClicks = session.ClickCount
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking click milestones for player {PlayerId}", session.PlayerId);
            }
        }

        private async Task CheckPotentialAchievementsAsync(GameSession session, BigNumber earnedAmount, ScoreUpdateSource source)
        {
            try
            {
                // Placeholder for achievement integration - would integrate with future Achievement service
                // This method provides the foundation for achievement checking

                var achievementChecks = new List<(string achievementId, bool condition, string name)>
                {
                    ("first_click", session.ClickCount == 1, "First Click"),
                    ("hundred_clicks", session.ClickCount == 100, "Century Clicker"),
                    ("thousand_points", session.Score >= new BigNumber(1000), "Thousand Club"),
                    ("speed_clicker", earnedAmount > new BigNumber(100) && source == ScoreUpdateSource.Click, "Speed Clicker"),
                    ("passive_earner", source == ScoreUpdateSource.PassiveIncome && earnedAmount > new BigNumber(50), "Passive Income")
                };

                foreach (var (achievementId, condition, name) in achievementChecks)
                {
                    if (condition)
                    {
                        // Check if this achievement has already been awarded
                        var hasAchievement = await HasPlayerAchievementAsync(session.PlayerId, achievementId);
                        if (!hasAchievement)
                        {
                            await AwardAchievementAsync(session.PlayerId, achievementId, name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking potential achievements for player {PlayerId}", session.PlayerId);
            }
        }

        private async Task ValidateClickPatternAsync(GameSession session)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Check if too soon since last click
                if (session.LastClickTime.HasValue &&
                    now - session.LastClickTime.Value < _clickRateLimit)
                {
                    throw new InvalidOperationException("Clicking too fast - please slow down");
                }

                // Update click tracking
                session.LastClickTime = now;
                session.ClicksInLastMinute++;

                // Reset minute counter if needed
                if (now - session.LastAntiCheatCheck > TimeSpan.FromMinutes(1))
                {
                    session.ClicksInLastMinute = 1;
                    session.LastAntiCheatCheck = now;
                }

                // Check for suspiciously high click rates
                if (session.ClicksInLastMinute > _maxClicksPerMinute)
                {
                    _logger.LogWarning("Suspicious click rate detected for player {PlayerId}: {ClicksPerMinute} clicks/minute",
                        session.PlayerId, session.ClicksInLastMinute);

                    await _notificationService.SendErrorNotificationAsync(new ErrorNotificationDto
                    {
                        Title = "⚠️ Click Rate Warning",
                        Message = "Your clicking rate seems unusually high. Please maintain a reasonable pace.",
                        TargetPlayerId = session.PlayerId,
                        ErrorType = "RateLimit",
                        IsRetryable = true
                    });

                    throw new InvalidOperationException("Click rate limit exceeded - suspicious activity detected");
                }
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw rate limiting exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating click pattern for player {PlayerId}", session.PlayerId);
            }
        }

        private async Task StoreScoreSnapshotAsync(GameSession session, ScoreUpdateSource source)
        {
            try
            {
                var snapshot = new ScoreSnapshot
                {
                    Score = session.Score.ToString(),
                    Timestamp = DateTime.UtcNow,
                    Source = source,
                    AdditionalInfo = $"Clicks: {session.ClickCount}, Power: {session.ClickPower}"
                };

                var historyKey = $"score_history:{session.PlayerId}";
                var serialized = JsonSerializer.Serialize(snapshot);

                await _cache.ListLeftPushAsync(historyKey, serialized);
                await _cache.KeyExpireAsync(historyKey, TimeSpan.FromDays(1));
                await _cache.ListTrimAsync(historyKey, 0, 999); // Keep last 1000 entries
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing score snapshot for player {PlayerId}", session.PlayerId);
            }
        }

        // Helper methods for achievements (placeholders for future integration)
        private async Task<bool> HasPlayerAchievementAsync(Guid playerId, string achievementId)
        {
            try
            {
                // Placeholder - would integrate with Achievement service
                var key = $"player_achievements:{playerId}";
                return await _cache.SetContainsAsync(key, achievementId);
            }
            catch
            {
                return false;
            }
        }

        private async Task AwardAchievementAsync(Guid playerId, string achievementId, string achievementName)
        {
            try
            {
                // Store achievement temporarily in cache (placeholder for Achievement service)
                var key = $"player_achievements:{playerId}";
                await _cache.SetAddAsync(key, achievementId);
                await _cache.KeyExpireAsync(key, TimeSpan.FromDays(365));

                // Send achievement notification
                await _notificationService.SendAchievementNotificationAsync(playerId, achievementId, achievementName,
                    $"You've unlocked the {achievementName} achievement!");

                _logger.LogBusinessEvent(_correlationService, "AchievementAwarded", new
                {
                    PlayerId = playerId,
                    AchievementId = achievementId,
                    AchievementName = achievementName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error awarding achievement {AchievementId} to player {PlayerId}", achievementId, playerId);
            }
        }

        // Utility methods
        private static int CalculatePlayerLevel(BigNumber score)
        {
            // Simple level calculation: every 1000 points = 1 level
            return Math.Max(1, (int)(score.ToDecimal() / 1000));
        }

        private static BigNumber CalculateLevelUpBonus(int level)
        {
            // Give bonus equal to level * 100
            return new BigNumber(level * 100);
        }

        private static string GetScoreUpdateTitle(ScoreUpdateSource source)
        {
            return source switch
            {
                ScoreUpdateSource.Click => "Click Reward",
                ScoreUpdateSource.PassiveIncome => "Passive Income",
                ScoreUpdateSource.Upgrade => "Upgrade Bonus",
                ScoreUpdateSource.Offline => "Welcome Back!",
                ScoreUpdateSource.Achievement => "Achievement Reward",
                ScoreUpdateSource.Event => "Event Bonus",
                _ => "Score Updated"
            };
        }

        private static string GetScoreUpdateMessage(ScoreUpdateSource source, BigNumber amount)
        {
            return source switch
            {
                ScoreUpdateSource.Click => $"Great click! You earned {amount} points!",
                ScoreUpdateSource.PassiveIncome => $"Your passive income generated {amount} points!",
                ScoreUpdateSource.Upgrade => $"Your upgrade is working! +{amount} points!",
                ScoreUpdateSource.Offline => $"While you were away, you earned {amount} points!",
                ScoreUpdateSource.Achievement => $"Achievement bonus: {amount} points!",
                ScoreUpdateSource.Event => $"Special event bonus: {amount} points!",
                _ => $"You earned {amount} points!"
            };
        }

        private static bool ShouldShowAnimation(ScoreUpdateSource source, BigNumber amount)
        {
            return source switch
            {
                ScoreUpdateSource.Click => true,
                ScoreUpdateSource.Achievement => true,
                ScoreUpdateSource.Event => true,
                ScoreUpdateSource.Upgrade => amount > new BigNumber(50),
                ScoreUpdateSource.PassiveIncome => amount > new BigNumber(100),
                _ => false
            };
        }

        private static string GetAnimationType(ScoreUpdateSource source, BigNumber amount)
        {
            return source switch
            {
                ScoreUpdateSource.Click when amount > new BigNumber(100) => "big_click",
                ScoreUpdateSource.Click => "normal_click",
                ScoreUpdateSource.Achievement => "achievement_sparkle",
                ScoreUpdateSource.Event => "event_burst",
                ScoreUpdateSource.Upgrade => "upgrade_glow",
                ScoreUpdateSource.PassiveIncome => "passive_flow",
                _ => "default"
            };
        }

        private static string BuildUpgradeEffectMessage(UpgradeEffectsDto upgradeEffects, GameSession session,
            BigNumber previousClickPower, decimal previousPassiveIncome)
        {
            var messages = new List<string>();

            if (upgradeEffects.ClickPowerBonus > 0)
            {
                messages.Add($"Click Power: {previousClickPower} → {session.ClickPower} (+{upgradeEffects.ClickPowerBonus})");
            }

            if (upgradeEffects.PassiveIncomeBonus > 0)
            {
                messages.Add($"Passive Income: {previousPassiveIncome:F2}/s → {session.PassiveIncomePerSecond:F2}/s (+{upgradeEffects.PassiveIncomeBonus:F2})");
            }

            if (upgradeEffects.MultiplierBonus > 1.0m)
            {
                messages.Add($"Applied {upgradeEffects.MultiplierBonus:F2}x multiplier!");
            }

            return string.Join("\n", messages);
        }

        private async Task<GameSession?> GetGameSessionFromCacheOrDb(Guid playerId)
        {
            var cacheKey = $"game_session:{playerId}";
            var cachedData = await _cache.StringGetAsync(cacheKey);

            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<GameSession>(cachedData!);
            }

            var session = await _context.GameSessions
                .FirstOrDefaultAsync(gs => gs.PlayerId == playerId && gs.IsActive);

            if (session != null)
            {
                await UpdateSessionInCache(session);
            }

            return session;
        }

        private async Task UpdateSessionInCache(GameSession session)
        {
            var cacheKey = $"game_session:{session.PlayerId}";
            var serializedSession = JsonSerializer.Serialize(session);
            await _cache.StringSetAsync(cacheKey, serializedSession, TimeSpan.FromMinutes(30));
        }

        #endregion
    }
}