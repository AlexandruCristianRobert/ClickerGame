namespace ClickerGame.GameCore.Domain.Enums
{
    public enum NotificationType
    {
        Achievement = 1,
        Upgrade = 2,
        Score = 3,
        System = 4,
        Event = 5,
        Presence = 6,
        Leaderboard = 7,
        Error = 8,
        Warning = 9,
        Success = 10,
        Maintenance = 11,     
        GoldenCookie = 12,
        Announcement = 13,
        Emergency = 14
    }

    public enum NotificationPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4,
        Urgent = 5
    }

    public enum NotificationTargetType
    {
        Individual = 1,     // Specific player
        Group = 2,          // Group of players (e.g., guild)
        Broadcast = 3,      // All players
        Role = 4            // Players with specific role
    }
}