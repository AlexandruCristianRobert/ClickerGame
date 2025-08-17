using ClickerGame.GameCore.Application.DTOs.Metrics;

namespace ClickerGame.GameCore.Application.Services
{
    public interface ISignalRMetricsService
    {
        // Connection Metrics
        Task RecordConnectionAsync(string connectionId, string transport, string userAgent, string ipAddress);
        Task RecordDisconnectionAsync(string connectionId, string reason, TimeSpan connectionDuration);
        Task RecordConnectionFailureAsync(string reason, string transport, string userAgent);
        Task RecordReconnectionAsync(string connectionId, int attemptNumber);

        // Message Metrics
        Task RecordMessageSentAsync(string connectionId, string methodName, int payloadSize, TimeSpan processingTime);
        Task RecordMessageReceivedAsync(string connectionId, string methodName, int payloadSize);
        Task RecordMessageFailureAsync(string connectionId, string methodName, string errorType);

        // Transport Metrics
        Task RecordTransportFallbackAsync(string connectionId, string fromTransport, string toTransport);
        Task RecordTransportUpgradeAsync(string connectionId, string fromTransport, string toTransport);

        // Performance Metrics
        Task RecordHubMethodLatencyAsync(string methodName, TimeSpan latency);
        Task RecordGroupOperationAsync(string operation, string groupName, int memberCount, TimeSpan duration);
        Task RecordBroadcastOperationAsync(string operation, int recipientCount, int payloadSize, TimeSpan duration);

        // Health Metrics
        Task<SignalRHealthMetrics> GetHealthMetricsAsync();
        Task<SignalRConnectionMetrics> GetConnectionMetricsAsync();
        Task<SignalRMessageMetrics> GetMessageMetricsAsync();
        Task<SignalRPerformanceMetrics> GetPerformanceMetricsAsync();
        Task<List<SignalRAlert>> GetActiveAlertsAsync();

        // Alerting
        Task CheckConnectionHealthAsync();
        Task CheckMessageThroughputAsync();
        Task CheckErrorRatesAsync();
    }
}