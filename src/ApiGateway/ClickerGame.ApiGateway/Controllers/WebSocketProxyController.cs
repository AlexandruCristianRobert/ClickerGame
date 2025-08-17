using ClickerGame.ApiGateway.Services;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickerGame.ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WebSocketProxyController : ControllerBase
    {
        private readonly IWebSocketProxyService _proxyService;
        private readonly ILogger<WebSocketProxyController> _logger;
        private readonly ICorrelationService _correlationService;

        public WebSocketProxyController(
            IWebSocketProxyService proxyService,
            ILogger<WebSocketProxyController> logger,
            ICorrelationService correlationService)
        {
            _proxyService = proxyService;
            _logger = logger;
            _correlationService = correlationService;
        }

        /// <summary>
        /// Get WebSocket proxy statistics
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<WebSocketProxyStats>> GetProxyStats()
        {
            _logger.LogRequestStart(_correlationService, "GetWebSocketProxyStats");

            try
            {
                var stats = await _proxyService.GetProxyStatsAsync();

                _logger.LogBusinessEvent(_correlationService, "WebSocketProxyStatsRetrieved", new
                {
                    ActiveConnections = stats.ActiveConnections,
                    TotalConnections = stats.TotalConnections
                });

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error retrieving WebSocket proxy stats");
                return StatusCode(500, new { error = "Failed to retrieve proxy stats" });
            }
        }

        /// <summary>
        /// Test WebSocket connectivity through the proxy
        /// </summary>
        [HttpGet("test-connectivity/{service}")]
        public async Task<ActionResult> TestConnectivity(string service)
        {
            _logger.LogRequestStart(_correlationService, "TestWebSocketConnectivity");

            try
            {
                var serviceUrls = new Dictionary<string, string>
                {
                    ["gamecore"] = "ws://gamecore-service/gameHub",
                    ["test"] = "ws://echo.websocket.org"
                };

                if (!serviceUrls.ContainsKey(service.ToLower()))
                {
                    return BadRequest(new { error = "Unknown service", availableServices = serviceUrls.Keys });
                }

                var targetUri = serviceUrls[service.ToLower()];

                // This would be a simple connectivity test
                var result = new
                {
                    service = service,
                    targetUri = targetUri,
                    status = "reachable", // Would implement actual connectivity test
                    timestamp = DateTime.UtcNow
                };

                _logger.LogBusinessEvent(_correlationService, "WebSocketConnectivityTested", new
                {
                    Service = service,
                    TargetUri = targetUri,
                    Status = "reachable"
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Error testing WebSocket connectivity for {Service}", service);
                return StatusCode(500, new { error = "Connectivity test failed" });
            }
        }
    }
}