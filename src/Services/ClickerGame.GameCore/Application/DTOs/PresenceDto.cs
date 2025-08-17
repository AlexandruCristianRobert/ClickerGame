namespace ClickerGame.GameCore.Application.DTOs
{
    public class PresenceDto
    {
        public Guid PlayerId { get; init; }
        public string Username { get; init; } = string.Empty;
        public PresenceStatus Status { get; init; } = PresenceStatus.Online;
        public DateTime LastSeen { get; init; } = DateTime.UtcNow;
        public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
        public string? CurrentActivity { get; init; }
        public Dictionary<string, object> Metadata { get; init; } = new();
        public int ConnectionCount { get; init; } = 1;
        public string? UserAgent { get; init; }
        public string? IpAddress { get; init; }
    }

    public class PresenceUpdateDto
    {
        public Guid PlayerId { get; init; }
        public string Username { get; init; } = string.Empty;
        public PresenceStatus Status { get; init; }
        public PresenceStatus PreviousStatus { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string? Activity { get; init; }
        public bool IsFirstConnection { get; init; }
        public bool IsLastDisconnection { get; init; }
    }

    public class OnlinePlayersDto
    {
        public int TotalOnline { get; init; }
        public List<PresenceDto> Players { get; init; } = new();
        public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
        public Dictionary<PresenceStatus, int> StatusCounts { get; init; } = new();
    }

    public class PlayerConnectionDto
    {
        public string ConnectionId { get; init; } = string.Empty;
        public Guid PlayerId { get; init; }
        public string Username { get; init; } = string.Empty;
        public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
        public string? UserAgent { get; init; }
        public string? IpAddress { get; init; }
        public bool IsActive { get; init; } = true;
    }

    public enum PresenceStatus
    {
        Offline = 0,
        Online = 1,
        Away = 2,
        Busy = 3,
        Invisible = 4
    }
}