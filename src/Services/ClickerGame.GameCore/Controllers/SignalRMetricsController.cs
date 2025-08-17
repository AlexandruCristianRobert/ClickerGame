using ClickerGame.GameCore.Application.DTOs.Metrics;
using ClickerGame.GameCore.Application.Services;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickerGame.GameCore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SignalRMetricsController : ControllerBase
    {
        private readonly ISignalRMetricsService _metricsService;
        private readonly ILogger<SignalRMetricsController> _logger;
        private readonly ICorrelationService _correlationService;

        public SignalRMetricsController(
            ISignalRMetricsService metricsService,
            ILogger<SignalRMetricsController> logger,
            ICorrelationService correlationService)
        {
            _metricsService = metricsService;
            _logger = logger;
            _correlationService = correlationService;
        }

        /// <summary>
        /// Get overall SignalR health metrics
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult<SignalRHealthMetrics>> GetHealthMetrics()
        {
            _logger.LogRequestStart(_correlationService, "GetSignalRHealthMetrics");

            try
            {
                var metrics = await _metricsService.GetHealthMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting SignalR health metrics");
                return StatusCode(500, new { error = "An error occurred while retrieving health metrics" });
            }
        }

        /// <summary>
        /// Get detailed connection metrics
        /// </summary>
        [HttpGet("connections")]
        public async Task<ActionResult<SignalRConnectionMetrics>> GetConnectionMetrics()
        {
            _logger.LogRequestStart(_correlationService, "GetSignalRConnectionMetrics");

            try
            {
                var metrics = await _metricsService.GetConnectionMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting SignalR connection metrics");
                return StatusCode(500, new { error = "An error occurred while retrieving connection metrics" });
            }
        }

        /// <summary>
        /// Get message throughput and latency metrics
        /// </summary>
        [HttpGet("messages")]
        public async Task<ActionResult<SignalRMessageMetrics>> GetMessageMetrics()
        {
            _logger.LogRequestStart(_correlationService, "GetSignalRMessageMetrics");

            try
            {
                var metrics = await _metricsService.GetMessageMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting SignalR message metrics");
                return StatusCode(500, new { error = "An error occurred while retrieving message metrics" });
            }
        }

        /// <summary>
        /// Get performance metrics including latency and resource usage
        /// </summary>
        [HttpGet("performance")]
        public async Task<ActionResult<SignalRPerformanceMetrics>> GetPerformanceMetrics()
        {
            _logger.LogRequestStart(_correlationService, "GetSignalRPerformanceMetrics");

            try
            {
                var metrics = await _metricsService.GetPerformanceMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting SignalR performance metrics");
                return StatusCode(500, new { error = "An error occurred while retrieving performance metrics" });
            }
        }

        /// <summary>
        /// Get active alerts for SignalR issues
        /// </summary>
        [HttpGet("alerts")]
        public async Task<ActionResult<List<SignalRAlert>>> GetActiveAlerts()
        {
            _logger.LogRequestStart(_correlationService, "GetSignalRActiveAlerts");

            try
            {
                var alerts = await _metricsService.GetActiveAlertsAsync();
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting SignalR active alerts");
                return StatusCode(500, new { error = "An error occurred while retrieving alerts" });
            }
        }

        /// <summary>
        /// Get comprehensive dashboard data
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<SignalRDashboardData>> GetDashboardData()
        {
            _logger.LogRequestStart(_correlationService, "GetSignalRDashboardData");

            try
            {
                var dashboard = new SignalRDashboardData
                {
                    HealthMetrics = await _metricsService.GetHealthMetricsAsync(),
                    ConnectionMetrics = await _metricsService.GetConnectionMetricsAsync(),
                    MessageMetrics = await _metricsService.GetMessageMetricsAsync(),
                    PerformanceMetrics = await _metricsService.GetPerformanceMetricsAsync(),
                    ActiveAlerts = await _metricsService.GetActiveAlertsAsync(),
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error getting SignalR dashboard data");
                return StatusCode(500, new { error = "An error occurred while retrieving dashboard data" });
            }
        }

        /// <summary>
        /// Trigger manual health checks
        /// </summary>
        [HttpPost("health-check")]
        public async Task<ActionResult> TriggerHealthCheck()
        {
            _logger.LogRequestStart(_correlationService, "TriggerSignalRHealthCheck");

            try
            {
                await _metricsService.CheckConnectionHealthAsync();
                await _metricsService.CheckMessageThroughputAsync();
                await _metricsService.CheckErrorRatesAsync();

                _logger.LogBusinessEvent(_correlationService, "SignalRHealthCheckTriggered", null);
                return Ok(new { message = "Health check completed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error triggering SignalR health check");
                return StatusCode(500, new { error = "An error occurred while triggering health check" });
            }
        }
    }

    public class SignalRDashboardData
    {
        public SignalRHealthMetrics HealthMetrics { get; set; } = new();
        public SignalRConnectionMetrics ConnectionMetrics { get; set; } = new();
        public SignalRMessageMetrics MessageMetrics { get; set; } = new();
        public SignalRPerformanceMetrics PerformanceMetrics { get; set; } = new();
        public List<SignalRAlert> ActiveAlerts { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }
}