namespace ClickerGame.GameCore.Application.DTOs.Metrics
{
    public class SignalRHealthMetrics
    {
        public int ActiveConnections { get; set; }
        public int TotalConnections { get; set; }
        public int ConnectionsLast5Minutes { get; set; }
        public int DisconnectionsLast5Minutes { get; set; }
        public double DisconnectionRate { get; set; }
        public double AverageConnectionDuration { get; set; }
        public Dictionary<string, int> TransportDistribution { get; set; } = new();
        public Dictionary<string, int> UserAgentDistribution { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public string OverallStatus { get; set; } = "Healthy";
    }

    public class SignalRConnectionMetrics
    {
        public int CurrentConnections { get; set; }
        public int PeakConnections { get; set; }
        public int TotalConnectionsToday { get; set; }
        public int SuccessfulConnections { get; set; }
        public int FailedConnections { get; set; }
        public double ConnectionSuccessRate { get; set; }
        public double AverageConnectionDuration { get; set; }
        public Dictionary<string, int> DisconnectionReasons { get; set; } = new();
        public Dictionary<string, int> ConnectionFailureReasons { get; set; } = new();
        public Dictionary<string, TransportMetrics> TransportMetrics { get; set; } = new();
        public List<ConnectionTrend> ConnectionTrends { get; set; } = new();
    }

    public class TransportMetrics
    {
        public string Transport { get; set; } = string.Empty;
        public int ActiveConnections { get; set; }
        public int TotalConnections { get; set; }
        public int FallbacksTo { get; set; }
        public int FallbacksFrom { get; set; }
        public double AverageLatency { get; set; }
        public int FailureCount { get; set; }
    }

    public class ConnectionTrend
    {
        public DateTime Timestamp { get; set; }
        public int ConnectionCount { get; set; }
        public int MessageCount { get; set; }
        public double AverageLatency { get; set; }
    }

    public class SignalRMessageMetrics
    {
        public long TotalMessagesSent { get; set; }
        public long TotalMessagesReceived { get; set; }
        public long MessagesPerSecond { get; set; }
        public long MessagesLast5Minutes { get; set; }
        public double AverageMessageSize { get; set; }
        public double AverageProcessingTime { get; set; }
        public int MessageFailures { get; set; }
        public double MessageFailureRate { get; set; }
        public Dictionary<string, MethodMetrics> MethodMetrics { get; set; } = new();
        public List<MessageThroughputTrend> ThroughputTrends { get; set; } = new();
    }

    public class MethodMetrics
    {
        public string MethodName { get; set; } = string.Empty;
        public long CallCount { get; set; }
        public double AverageLatency { get; set; }
        public double MaxLatency { get; set; }
        public double AveragePayloadSize { get; set; }
        public int FailureCount { get; set; }
        public double FailureRate { get; set; }
        public long CallsPerSecond { get; set; }
    }

    public class MessageThroughputTrend
    {
        public DateTime Timestamp { get; set; }
        public long MessageCount { get; set; }
        public double AverageLatency { get; set; }
        public int FailureCount { get; set; }
    }

    public class SignalRPerformanceMetrics
    {
        public double AverageHubMethodLatency { get; set; }
        public double MaxHubMethodLatency { get; set; }
        public double AverageBroadcastLatency { get; set; }
        public double MaxBroadcastLatency { get; set; }
        public Dictionary<string, double> MethodLatencies { get; set; } = new();
        public int ConcurrentOperations { get; set; }
        public int MaxConcurrentOperations { get; set; }
        public long MemoryUsage { get; set; }
        public double CpuUsage { get; set; }
        public Dictionary<string, GroupMetrics> GroupMetrics { get; set; } = new();
    }

    public class GroupMetrics
    {
        public string GroupName { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int MessagesPerSecond { get; set; }
        public double AverageLatency { get; set; }
        public int OperationCount { get; set; }
    }

    public class SignalRAlert
    {
        public string AlertId { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public enum SignalRAlertType
    {
        HighDisconnectionRate,
        LowConnectionSuccessRate,
        HighMessageFailureRate,
        HighLatency,
        ConnectionThresholdExceeded,
        TransportFallbackSpike,
        MemoryUsageHigh,
        CpuUsageHigh
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }
}