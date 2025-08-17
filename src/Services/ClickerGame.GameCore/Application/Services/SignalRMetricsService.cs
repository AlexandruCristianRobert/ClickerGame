using ClickerGame.GameCore.Application.DTOs.Metrics;
using ClickerGame.Shared.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace ClickerGame.GameCore.Application.Services
{
    public class SignalRMetricsService : ISignalRMetricsService
    {
        private readonly IDatabase _cache;
        private readonly ILogger<SignalRMetricsService> _logger;
        private readonly ICorrelationService _correlationService;

        // In-memory tracking for performance
        private readonly ConcurrentDictionary<string, ConnectionMetric> _activeConnections = new();
        private readonly ConcurrentQueue<MetricEvent> _metricEvents = new();
        private readonly Timer _flushTimer;

        private const string MetricsPrefix = "signalr_metrics";
        private const string AlertsPrefix = "signalr_alerts";

        public SignalRMetricsService(
            IConnectionMultiplexer redis,
            ILogger<SignalRMetricsService> logger,
            ICorrelationService correlationService)
        {
            _cache = redis.GetDatabase();
            _logger = logger;
            _correlationService = correlationService;

            // Flush metrics to Redis every 10 seconds
            _flushTimer = new Timer(FlushMetricsToRedis, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        #region Connection Metrics

        public async Task RecordConnectionAsync(string connectionId, string transport, string userAgent, string ipAddress)
        {
            try
            {
                var connectionMetric = new ConnectionMetric
                {
                    ConnectionId = connectionId,
                    Transport = transport,
                    UserAgent = userAgent,
                    IpAddress = ipAddress,
                    ConnectedAt = DateTime.UtcNow
                };

                _activeConnections.TryAdd(connectionId, connectionMetric);

                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "connection_established",
                    Timestamp = DateTime.UtcNow,
                    Data = new { connectionId, transport, userAgent }
                });

                // Update Redis counters immediately for critical metrics
                await _cache.StringIncrementAsync($"{MetricsPrefix}:connections:total");
                await _cache.StringIncrementAsync($"{MetricsPrefix}:connections:active");
                await _cache.HashIncrementAsync($"{MetricsPrefix}:transports", transport);

                _logger.LogInformation("SignalR connection established: {ConnectionId} via {Transport}",
                    connectionId, transport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR connection metric");
            }
        }

        public async Task RecordDisconnectionAsync(string connectionId, string reason, TimeSpan connectionDuration)
        {
            try
            {
                _activeConnections.TryRemove(connectionId, out var connectionMetric);

                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "connection_closed",
                    Timestamp = DateTime.UtcNow,
                    Data = new { connectionId, reason, durationSeconds = connectionDuration.TotalSeconds }
                });

                await _cache.StringDecrementAsync($"{MetricsPrefix}:connections:active");
                await _cache.HashIncrementAsync($"{MetricsPrefix}:disconnection_reasons", reason);
                await _cache.ListLeftPushAsync($"{MetricsPrefix}:connection_durations", connectionDuration.TotalSeconds);
                await _cache.ListTrimAsync($"{MetricsPrefix}:connection_durations", 0, 999); // Keep last 1000

                _logger.LogInformation("SignalR connection closed: {ConnectionId}, Duration: {Duration}, Reason: {Reason}",
                    connectionId, connectionDuration, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR disconnection metric");
            }
        }

        public async Task RecordConnectionFailureAsync(string reason, string transport, string userAgent)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "connection_failed",
                    Timestamp = DateTime.UtcNow,
                    Data = new { reason, transport, userAgent }
                });

                await _cache.StringIncrementAsync($"{MetricsPrefix}:connections:failures");
                await _cache.HashIncrementAsync($"{MetricsPrefix}:failure_reasons", reason);

                _logger.LogWarning("SignalR connection failed: {Reason} via {Transport}", reason, transport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR connection failure metric");
            }
        }

        public async Task RecordReconnectionAsync(string connectionId, int attemptNumber)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "reconnection",
                    Timestamp = DateTime.UtcNow,
                    Data = new { connectionId, attemptNumber }
                });

                await _cache.StringIncrementAsync($"{MetricsPrefix}:reconnections:total");
                await _cache.HashIncrementAsync($"{MetricsPrefix}:reconnection_attempts", attemptNumber.ToString());

                _logger.LogInformation("SignalR reconnection: {ConnectionId}, Attempt: {AttemptNumber}",
                    connectionId, attemptNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR reconnection metric");
            }
        }

        #endregion

        #region Message Metrics

        public async Task RecordMessageSentAsync(string connectionId, string methodName, int payloadSize, TimeSpan processingTime)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "message_sent",
                    Timestamp = DateTime.UtcNow,
                    Data = new { connectionId, methodName, payloadSize, processingTimeMs = processingTime.TotalMilliseconds }
                });

                // Update connection-specific metrics
                if (_activeConnections.TryGetValue(connectionId, out var connection))
                {
                    connection.MessagesSent++;
                    connection.TotalBytesSent += payloadSize;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR message sent metric");
            }
        }

        public async Task RecordMessageReceivedAsync(string connectionId, string methodName, int payloadSize)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "message_received",
                    Timestamp = DateTime.UtcNow,
                    Data = new { connectionId, methodName, payloadSize }
                });

                // Update connection-specific metrics
                if (_activeConnections.TryGetValue(connectionId, out var connection))
                {
                    connection.MessagesReceived++;
                    connection.TotalBytesReceived += payloadSize;
                }

                await _cache.StringIncrementAsync($"{MetricsPrefix}:messages:received");
                await _cache.HashIncrementAsync($"{MetricsPrefix}:method_calls", methodName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR message received metric");
            }
        }

        public async Task RecordMessageFailureAsync(string connectionId, string methodName, string errorType)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "message_failed",
                    Timestamp = DateTime.UtcNow,
                    Data = new { connectionId, methodName, errorType }
                });

                await _cache.StringIncrementAsync($"{MetricsPrefix}:messages:failures");
                await _cache.HashIncrementAsync($"{MetricsPrefix}:error_types", errorType);

                _logger.LogWarning("SignalR message failure: {ConnectionId}, Method: {MethodName}, Error: {ErrorType}",
                    connectionId, methodName, errorType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR message failure metric");
            }
        }

        #endregion

        #region Transport Metrics

        public async Task RecordTransportFallbackAsync(string connectionId, string fromTransport, string toTransport)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "transport_fallback",
                    Timestamp = DateTime.UtcNow,
                    Data = new { connectionId, fromTransport, toTransport }
                });

                await _cache.StringIncrementAsync($"{MetricsPrefix}:transport:fallbacks");
                await _cache.HashIncrementAsync($"{MetricsPrefix}:fallback_paths", $"{fromTransport}->{toTransport}");

                _logger.LogInformation("SignalR transport fallback: {ConnectionId} from {FromTransport} to {ToTransport}",
                    connectionId, fromTransport, toTransport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR transport fallback metric");
            }
        }

        public async Task RecordTransportUpgradeAsync(string connectionId, string fromTransport, string toTransport)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "transport_upgrade",
                    Timestamp = DateTime.UtcNow,
                    Data = new { connectionId, fromTransport, toTransport }
                });

                await _cache.StringIncrementAsync($"{MetricsPrefix}:transport:upgrades");

                _logger.LogInformation("SignalR transport upgrade: {ConnectionId} from {FromTransport} to {ToTransport}",
                    connectionId, fromTransport, toTransport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR transport upgrade metric");
            }
        }

        #endregion

        #region Performance Metrics

        public async Task RecordHubMethodLatencyAsync(string methodName, TimeSpan latency)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "hub_method_latency",
                    Timestamp = DateTime.UtcNow,
                    Data = new { methodName, latencyMs = latency.TotalMilliseconds }
                });

                await _cache.ListLeftPushAsync($"{MetricsPrefix}:latencies:{methodName}", latency.TotalMilliseconds);
                await _cache.ListTrimAsync($"{MetricsPrefix}:latencies:{methodName}", 0, 99); // Keep last 100
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR hub method latency metric");
            }
        }

        public async Task RecordGroupOperationAsync(string operation, string groupName, int memberCount, TimeSpan duration)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "group_operation",
                    Timestamp = DateTime.UtcNow,
                    Data = new { operation, groupName, memberCount, durationMs = duration.TotalMilliseconds }
                });

                await _cache.HashIncrementAsync($"{MetricsPrefix}:group_operations", operation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR group operation metric");
            }
        }

        public async Task RecordBroadcastOperationAsync(string operation, int recipientCount, int payloadSize, TimeSpan duration)
        {
            try
            {
                _metricEvents.Enqueue(new MetricEvent
                {
                    Type = "broadcast_operation",
                    Timestamp = DateTime.UtcNow,
                    Data = new { operation, recipientCount, payloadSize, durationMs = duration.TotalMilliseconds }
                });

                await _cache.StringIncrementAsync($"{MetricsPrefix}:broadcasts:total");
                await _cache.ListLeftPushAsync($"{MetricsPrefix}:broadcast_latencies", duration.TotalMilliseconds);
                await _cache.ListTrimAsync($"{MetricsPrefix}:broadcast_latencies", 0, 99);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording SignalR broadcast operation metric");
            }
        }

        #endregion

        #region Metrics Retrieval

        public async Task<SignalRHealthMetrics> GetHealthMetricsAsync()
        {
            try
            {
                var activeConnections = await _cache.StringGetAsync($"{MetricsPrefix}:connections:active");
                var totalConnections = await _cache.StringGetAsync($"{MetricsPrefix}:connections:total");
                var failures = await _cache.StringGetAsync($"{MetricsPrefix}:connections:failures");

                var transportStats = await _cache.HashGetAllAsync($"{MetricsPrefix}:transports");
                var disconnectionReasons = await _cache.HashGetAllAsync($"{MetricsPrefix}:disconnection_reasons");

                return new SignalRHealthMetrics
                {
                    ActiveConnections = activeConnections.HasValue ? (int)activeConnections : 0,
                    TotalConnections = totalConnections.HasValue ? (int)totalConnections : 0,
                    DisconnectionRate = CalculateDisconnectionRate(),
                    TransportDistribution = transportStats.ToDictionary(x => x.Name.ToString(), x => (int)x.Value),
                    LastUpdated = DateTime.UtcNow,
                    OverallStatus = DetermineHealthStatus()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SignalR health metrics");
                return new SignalRHealthMetrics { OverallStatus = "Unknown" };
            }
        }

        public async Task<SignalRConnectionMetrics> GetConnectionMetricsAsync()
        {
            try
            {
                var metrics = new SignalRConnectionMetrics
                {
                    CurrentConnections = _activeConnections.Count,
                    TotalConnectionsToday = await GetTodayConnectionCount(),
                    ConnectionTrends = await GetConnectionTrends()
                };

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SignalR connection metrics");
                return new SignalRConnectionMetrics();
            }
        }

        public async Task<SignalRMessageMetrics> GetMessageMetricsAsync()
        {
            try
            {
                var totalSent = await _cache.StringGetAsync($"{MetricsPrefix}:messages:sent");
                var totalReceived = await _cache.StringGetAsync($"{MetricsPrefix}:messages:received");
                var failures = await _cache.StringGetAsync($"{MetricsPrefix}:messages:failures");

                return new SignalRMessageMetrics
                {
                    TotalMessagesSent = totalSent.HasValue ? (long)totalSent : 0,
                    TotalMessagesReceived = totalReceived.HasValue ? (long)totalReceived : 0,
                    MessageFailures = failures.HasValue ? (int)failures : 0,
                    ThroughputTrends = await GetMessageThroughputTrends()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SignalR message metrics");
                return new SignalRMessageMetrics();
            }
        }

        public async Task<SignalRPerformanceMetrics> GetPerformanceMetricsAsync()
        {
            try
            {
                return new SignalRPerformanceMetrics
                {
                    ConcurrentOperations = _activeConnections.Count,
                    MethodLatencies = await GetMethodLatencies(),
                    MemoryUsage = GC.GetTotalMemory(false)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SignalR performance metrics");
                return new SignalRPerformanceMetrics();
            }
        }

        public async Task<List<SignalRAlert>> GetActiveAlertsAsync()
        {
            try
            {
                var alertKeys = await _cache.SetMembersAsync($"{AlertsPrefix}:active");
                var alerts = new List<SignalRAlert>();

                foreach (var key in alertKeys)
                {
                    var alertJson = await _cache.StringGetAsync($"{AlertsPrefix}:{key}");
                    if (alertJson.HasValue)
                    {
                        var alert = JsonSerializer.Deserialize<SignalRAlert>(alertJson!);
                        if (alert != null)
                        {
                            alerts.Add(alert);
                        }
                    }
                }

                return alerts.OrderByDescending(a => a.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SignalR active alerts");
                return new List<SignalRAlert>();
            }
        }

        #endregion

        #region Alerting

        public async Task CheckConnectionHealthAsync()
        {
            try
            {
                var currentConnections = _activeConnections.Count;
                var maxConnections = 1000; // Configure this value

                if (currentConnections > maxConnections * 0.9) // 90% threshold
                {
                    await CreateAlertAsync(SignalRAlertType.ConnectionThresholdExceeded,
                        AlertSeverity.Warning,
                        $"Connection count ({currentConnections}) approaching limit ({maxConnections})",
                        new { currentConnections, maxConnections });
                }

                var disconnectionRate = CalculateDisconnectionRate();
                if (disconnectionRate > 0.1) // 10% disconnection rate
                {
                    await CreateAlertAsync(SignalRAlertType.HighDisconnectionRate,
                        AlertSeverity.Critical,
                        $"High disconnection rate detected: {disconnectionRate:P2}",
                        new { disconnectionRate });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking SignalR connection health");
            }
        }

        public async Task CheckMessageThroughputAsync()
        {
            try
            {
                var recentFailures = await GetRecentMessageFailures();
                var totalMessages = await GetRecentMessageCount();

                if (totalMessages > 0)
                {
                    var failureRate = (double)recentFailures / totalMessages;
                    if (failureRate > 0.05) // 5% failure rate
                    {
                        await CreateAlertAsync(SignalRAlertType.HighMessageFailureRate,
                            AlertSeverity.Warning,
                            $"High message failure rate detected: {failureRate:P2}",
                            new { failureRate, recentFailures, totalMessages });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking SignalR message throughput");
            }
        }

        public async Task CheckErrorRatesAsync()
        {
            try
            {
                // Implementation for checking various error rates
                var connectionFailures = await _cache.StringGetAsync($"{MetricsPrefix}:connections:failures");
                var totalConnectionAttempts = await _cache.StringGetAsync($"{MetricsPrefix}:connections:total");

                if (connectionFailures.HasValue && totalConnectionAttempts.HasValue)
                {
                    var failureRate = (double)connectionFailures / (double)totalConnectionAttempts;
                    if (failureRate > 0.05) // 5% connection failure rate
                    {
                        await CreateAlertAsync(SignalRAlertType.LowConnectionSuccessRate,
                            AlertSeverity.Warning,
                            $"Low connection success rate: {(1 - failureRate):P2}",
                            new { failureRate });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking SignalR error rates");
            }
        }

        #endregion

        #region Private Helper Methods

        private async void FlushMetricsToRedis(object? state)
        {
            try
            {
                var events = new List<MetricEvent>();
                while (_metricEvents.TryDequeue(out var metricEvent))
                {
                    events.Add(metricEvent);
                }

                if (events.Any())
                {
                    await ProcessMetricEvents(events);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing SignalR metrics to Redis");
            }
        }

        private async Task ProcessMetricEvents(List<MetricEvent> events)
        {
            foreach (var eventGroup in events.GroupBy(e => e.Type))
            {
                switch (eventGroup.Key)
                {
                    case "message_sent":
                        await _cache.StringIncrementAsync($"{MetricsPrefix}:messages:sent", eventGroup.Count());
                        break;
                    case "message_received":
                        await _cache.StringIncrementAsync($"{MetricsPrefix}:messages:received", eventGroup.Count());
                        break;
                    case "message_failed":
                        await _cache.StringIncrementAsync($"{MetricsPrefix}:messages:failures", eventGroup.Count());
                        break;
                }
            }

            // Store detailed events for analysis
            var batch = _cache.CreateBatch();
            foreach (var evt in events.Take(100)) // Limit to prevent memory issues
            {
                var eventJson = JsonSerializer.Serialize(evt);
                batch.ListLeftPushAsync($"{MetricsPrefix}:events:{evt.Type}", eventJson);
                batch.ListTrimAsync($"{MetricsPrefix}:events:{evt.Type}", 0, 999);
            }
            batch.Execute();
        }

        private double CalculateDisconnectionRate()
        {
            var now = DateTime.UtcNow;
            var recentDisconnections = _metricEvents
                .Where(e => e.Type == "connection_closed" && e.Timestamp > now.AddMinutes(-5))
                .Count();

            var recentConnections = _metricEvents
                .Where(e => e.Type == "connection_established" && e.Timestamp > now.AddMinutes(-5))
                .Count();

            if (recentConnections == 0) return 0;
            return (double)recentDisconnections / recentConnections;
        }

        private string DetermineHealthStatus()
        {
            var connectionCount = _activeConnections.Count;
            var disconnectionRate = CalculateDisconnectionRate();

            if (disconnectionRate > 0.2) return "Critical";
            if (disconnectionRate > 0.1 || connectionCount > 800) return "Warning";
            return "Healthy";
        }

        private async Task<int> GetTodayConnectionCount()
        {
            var today = DateTime.UtcNow.Date.ToString("yyyyMMdd");
            var count = await _cache.StringGetAsync($"{MetricsPrefix}:daily:{today}:connections");
            return count.HasValue ? (int)count : 0;
        }

        private async Task<List<ConnectionTrend>> GetConnectionTrends()
        {
            // Implement connection trend calculation
            return new List<ConnectionTrend>();
        }

        private async Task<List<MessageThroughputTrend>> GetMessageThroughputTrends()
        {
            // Implement message throughput trend calculation
            return new List<MessageThroughputTrend>();
        }

        private async Task<Dictionary<string, double>> GetMethodLatencies()
        {
            var latencies = new Dictionary<string, double>();
            // Implement method latency calculation
            return latencies;
        }

        private async Task<int> GetRecentMessageFailures()
        {
            var failures = await _cache.StringGetAsync($"{MetricsPrefix}:messages:failures:recent");
            return failures.HasValue ? (int)failures : 0;
        }

        private async Task<int> GetRecentMessageCount()
        {
            var count = await _cache.StringGetAsync($"{MetricsPrefix}:messages:total:recent");
            return count.HasValue ? (int)count : 0;
        }

        private async Task CreateAlertAsync(SignalRAlertType alertType, AlertSeverity severity, string message, object data)
        {
            var alert = new SignalRAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = alertType.ToString(),
                Severity = severity.ToString(),
                Message = message,
                Category = "SignalR",
                Timestamp = DateTime.UtcNow,
                Data = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(data)) ?? new(),
                IsActive = true
            };

            var alertJson = JsonSerializer.Serialize(alert);
            await _cache.StringSetAsync($"{AlertsPrefix}:{alert.AlertId}", alertJson, TimeSpan.FromHours(24));
            await _cache.SetAddAsync($"{AlertsPrefix}:active", alert.AlertId);

            _logger.LogWarning("SignalR Alert Created: {AlertType} - {Message}", alertType, message);
        }

        #endregion

        #region Helper Classes

        private class ConnectionMetric
        {
            public string ConnectionId { get; set; } = string.Empty;
            public string Transport { get; set; } = string.Empty;
            public string UserAgent { get; set; } = string.Empty;
            public string IpAddress { get; set; } = string.Empty;
            public DateTime ConnectedAt { get; set; }
            public int MessagesSent { get; set; }
            public int MessagesReceived { get; set; }
            public long TotalBytesSent { get; set; }
            public long TotalBytesReceived { get; set; }
        }

        private class MetricEvent
        {
            public string Type { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
            public object Data { get; set; } = new();
        }

        #endregion
    }
}