using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ClickerGame.GameCore.Controllers
{
    [ApiController]
    [Route("api/signalr")]
    public class SignalRTelemetryController : ControllerBase
    {
        private readonly ILogger<SignalRTelemetryController> _logger;
        private readonly ICorrelationService _correlationService;

        public SignalRTelemetryController(
            ILogger<SignalRTelemetryController> logger,
            ICorrelationService correlationService)
        {
            _logger = logger;
            _correlationService = correlationService;
        }

        /// <summary>
        /// Receive telemetry data from SignalR clients for transport monitoring
        /// </summary>
        [HttpPost("telemetry")]
        public ActionResult ReceiveTelemetry([FromBody] SignalRTelemetryData telemetryData)
        {
            try
            {
                // Log different types of events with appropriate levels
                switch (telemetryData.Event?.ToLower())
                {
                    case "transportsuccess":
                        _logger.LogInformation("SignalR Transport Success: {Transport} for {Browser} {Version}",
                            telemetryData.Transport,
                            telemetryData.BrowserInfo?.Name,
                            telemetryData.BrowserInfo?.Version);

                        _logger.LogBusinessEvent(_correlationService, "SignalRTransportSuccess", new
                        {
                            Transport = telemetryData.Transport,
                            BrowserInfo = telemetryData.BrowserInfo,
                            ConnectionId = telemetryData.ConnectionId,
                            Timestamp = telemetryData.Timestamp
                        });
                        break;

                    case "transportfailure":
                        _logger.LogWarning("SignalR Transport Failure: {Transport} for {Browser} {Version} - Error: {Error}",
                            telemetryData.Transport,
                            telemetryData.BrowserInfo?.Name,
                            telemetryData.BrowserInfo?.Version,
                            telemetryData.Error);

                        _logger.LogBusinessEvent(_correlationService, "SignalRTransportFailure", new
                        {
                            Transport = telemetryData.Transport,
                            Error = telemetryData.Error,
                            BrowserInfo = telemetryData.BrowserInfo,
                            Timestamp = telemetryData.Timestamp
                        });
                        break;

                    case "reconnecting":
                        _logger.LogInformation("SignalR Reconnecting: {ConnectionId}", telemetryData.ConnectionId);

                        _logger.LogBusinessEvent(_correlationService, "SignalRReconnecting", new
                        {
                            ConnectionId = telemetryData.ConnectionId,
                            Data = telemetryData.Data,
                            Timestamp = telemetryData.Timestamp
                        });
                        break;

                    case "reconnected":
                        _logger.LogInformation("SignalR Reconnected: {ConnectionId}", telemetryData.ConnectionId);

                        _logger.LogBusinessEvent(_correlationService, "SignalRReconnected", new
                        {
                            ConnectionId = telemetryData.ConnectionId,
                            Data = telemetryData.Data,
                            Timestamp = telemetryData.Timestamp
                        });
                        break;

                    case "connectionclosed":
                        var isGraceful = telemetryData.Data?.GetProperty("graceful").GetBoolean() ?? false;

                        if (isGraceful)
                        {
                            _logger.LogInformation("SignalR Connection Closed Gracefully: {ConnectionId}", telemetryData.ConnectionId);
                        }
                        else
                        {
                            _logger.LogWarning("SignalR Connection Closed with Error: {ConnectionId}", telemetryData.ConnectionId);
                        }

                        _logger.LogBusinessEvent(_correlationService, "SignalRConnectionClosed", new
                        {
                            ConnectionId = telemetryData.ConnectionId,
                            IsGraceful = isGraceful,
                            Data = telemetryData.Data,
                            Timestamp = telemetryData.Timestamp
                        });
                        break;

                    case "latencymeasured":
                        var latency = telemetryData.Data?.GetProperty("latencyMs").GetDouble() ?? 0;

                        if (latency > 1000) // Log slow connections
                        {
                            _logger.LogWarning("High SignalR Latency: {Latency}ms for {ConnectionId}",
                                latency, telemetryData.ConnectionId);
                        }
                        else
                        {
                            _logger.LogDebug("SignalR Latency: {Latency}ms for {ConnectionId}",
                                latency, telemetryData.ConnectionId);
                        }

                        _logger.LogBusinessEvent(_correlationService, "SignalRLatencyMeasured", new
                        {
                            ConnectionId = telemetryData.ConnectionId,
                            LatencyMs = latency,
                            Timestamp = telemetryData.Timestamp
                        });
                        break;

                    default:
                        _logger.LogDebug("SignalR Telemetry: {Event} for {ConnectionId}",
                            telemetryData.Event, telemetryData.ConnectionId);
                        break;
                }

                return Ok(new { success = true, message = "Telemetry data received" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SignalR telemetry data");
                return StatusCode(500, new { error = "Failed to process telemetry data" });
            }
        }

        /// <summary>
        /// Get SignalR transport statistics for monitoring dashboard
        /// </summary>
        [HttpGet("transport-stats")]
        public ActionResult GetTransportStatistics([FromQuery] DateTime? fromDate = null)
        {
            try
            {
                // This would typically query a metrics database
                // For now, return mock data showing the concept

                var stats = new
                {
                    Period = fromDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    TransportSuccessRates = new
                    {
                        WebSockets = 0.92,
                        ServerSentEvents = 0.85,
                        LongPolling = 0.98
                    },
                    BrowserCompatibility = new
                    {
                        Chrome = new { SuccessRate = 0.95, CommonTransport = "WebSockets" },
                        Firefox = new { SuccessRate = 0.93, CommonTransport = "WebSockets" },
                        Safari = new { SuccessRate = 0.88, CommonTransport = "ServerSentEvents" },
                        Edge = new { SuccessRate = 0.91, CommonTransport = "WebSockets" },
                        InternetExplorer = new { SuccessRate = 0.75, CommonTransport = "LongPolling" }
                    },
                    AverageLatency = new
                    {
                        WebSockets = 45.2,
                        ServerSentEvents = 78.5,
                        LongPolling = 156.8
                    },
                    ReconnectionStats = new
                    {
                        AverageReconnectTime = 2.3,
                        SuccessfulReconnects = 0.87,
                        FailedReconnects = 0.13
                    }
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transport statistics");
                return StatusCode(500, new { error = "Failed to get transport statistics" });
            }
        }
    }

    // DTOs for telemetry data
    public class SignalRTelemetryData
    {
        public string? Event { get; set; }
        public string? Transport { get; set; }
        public string? Error { get; set; }
        public BrowserInfo? BrowserInfo { get; set; }
        public string? ConnectionId { get; set; }
        public DateTime Timestamp { get; set; }
        public JsonElement? Data { get; set; }
    }

    public class BrowserInfo
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? UserAgent { get; set; }
        public bool SupportsWebSockets { get; set; }
        public bool SupportsServerSentEvents { get; set; }
    }
}