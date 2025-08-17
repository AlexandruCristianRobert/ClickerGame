using ClickerGame.GameCore.Domain.ValueObjects;

namespace ClickerGame.GameCore.Application.DTOs
{
    public class ScoreUpdateDto
    {
        public Guid PlayerId { get; init; }
        public string CurrentScore { get; init; } = string.Empty;
        public string PreviousScore { get; init; } = string.Empty;
        public string EarnedAmount { get; init; } = string.Empty;
        public long ClickCount { get; init; }
        public string ClickPower { get; init; } = string.Empty;
        public decimal PassiveIncome { get; init; }
        public ScoreUpdateSource Source { get; init; } = ScoreUpdateSource.Click;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public bool IsMultiplierActive { get; init; } = false;
        public decimal ActiveMultiplier { get; init; } = 1.0m;
        public Dictionary<string, object> AdditionalData { get; init; } = new();
    }

    public class LiveScoreUpdateDto
    {
        public Guid PlayerId { get; init; }
        public string Score { get; init; } = string.Empty;
        public string Delta { get; init; } = string.Empty;
        public long ClickCount { get; init; }
        public string ClickPower { get; init; } = string.Empty;
        public decimal PassiveIncome { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public ScoreUpdateSource Source { get; init; } = ScoreUpdateSource.Click;
        public bool ShowAnimation { get; init; } = true;
        public string? AnimationType { get; init; }
    }

    public class ScoreHistoryDto
    {
        public Guid PlayerId { get; init; }
        public List<ScoreSnapshot> ScoreHistory { get; init; } = new();
        public TimeSpan TimeRange { get; init; }
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    }

    public class ScoreSnapshot
    {
        public string Score { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
        public ScoreUpdateSource Source { get; init; }
        public string? AdditionalInfo { get; init; }
    }

    public class ScoreLeaderboardUpdateDto
    {
        public Guid PlayerId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Score { get; init; } = string.Empty;
        public int Rank { get; init; }
        public int PreviousRank { get; init; }
        public bool RankChanged { get; init; }
        public string RankChangeDirection { get; init; } = string.Empty; // "up", "down", "none"
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    public enum ScoreUpdateSource
    {
        Click = 1,
        PassiveIncome = 2,
        Upgrade = 3,
        Achievement = 4,
        Event = 5,
        Admin = 6,
        Multiplier = 7,
        GoldenCookie = 8,
        Offline = 9
    }
}