using ClickerGame.GameCore.Application.DTOs.Notifications;
using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.Services
{
    public interface IGameNotificationService
    {
        // Generic notification methods
        Task SendNotificationAsync<T>(T notification) where T : BaseNotificationDto;
        Task SendNotificationToPlayerAsync<T>(Guid playerId, T notification) where T : BaseNotificationDto;
        Task SendNotificationToGroupAsync<T>(string groupName, T notification) where T : BaseNotificationDto;
        Task BroadcastNotificationAsync<T>(T notification) where T : BaseNotificationDto;

        // Specific notification methods (backward compatibility)
        Task SendScoreUpdateAsync(Guid playerId, string score, long clickCount, string clickPower, decimal passiveIncome);
        Task SendAchievementNotificationAsync(Guid playerId, string achievementId, string title, string description);
        Task SendUpgradePurchaseNotificationAsync(Guid playerId, string upgradeName, int newLevel, string totalCost);
        Task BroadcastSystemMessageAsync(string message, string messageType = "info");
        Task BroadcastEventNotificationAsync(string eventName, object eventData);
        Task SendPlayerPresenceUpdateAsync(Guid playerId, bool isOnline);
        Task SendOnlinePlayersCountAsync();

        // Enhanced notification methods
        Task SendScoreUpdateNotificationAsync(ScoreNotificationDto notification);
        Task SendAchievementNotificationAsync(AchievementNotificationDto notification);
        Task SendUpgradeNotificationAsync(UpgradeNotificationDto notification);
        Task SendSystemNotificationAsync(SystemNotificationDto notification);
        Task SendPresenceNotificationAsync(PresenceNotificationDto notification);
        Task SendEventNotificationAsync(EventNotificationDto notification);
        Task SendErrorNotificationAsync(ErrorNotificationDto notification);

        // Notification management
        Task<bool> IsPlayerOnlineAsync(Guid playerId);
        Task<int> GetOnlinePlayersCountAsync();
        Task MarkNotificationAsReadAsync(Guid playerId, string notificationId);
        Task ClearPlayerNotificationsAsync(Guid playerId);
        Task<IEnumerable<BaseNotificationDto>> GetUnreadNotificationsAsync(Guid playerId);

        Task SendAchievementProgressUpdateAsync(AchievementProgressNotificationDto notification);
        Task SendAchievementUnlockedWithCelebrationAsync(AchievementNotificationDto notification);
        Task SendAchievementSeriesCompletedAsync(Guid playerId, string seriesName, List<string> completedAchievements, AchievementReward? seriesReward = null);
        Task SendDailyAchievementSummaryAsync(Guid playerId, List<AchievementNotificationDto> dailyAchievements);

        // Achievement history and queue management
        Task<IEnumerable<AchievementNotificationDto>> GetPlayerAchievementHistoryAsync(Guid playerId, int pageSize = 50, int pageNumber = 1);
        Task<int> GetUnreadAchievementCountAsync(Guid playerId);
        Task MarkAchievementAsSeenAsync(Guid playerId, string achievementId);
        Task<IEnumerable<AchievementNotificationDto>> GetRecentAchievementsAsync(Guid playerId, TimeSpan? timeframe = null);

        Task SendMaintenanceNotificationAsync(MaintenanceNotificationDto notification);
        Task SendEventCountdownNotificationAsync(EventCountdownNotificationDto notification);
        Task SendGoldenCookieNotificationAsync(GoldenCookieNotificationDto notification);
        Task SendAnnouncementNotificationAsync(AnnouncementNotificationDto notification);
    }
}