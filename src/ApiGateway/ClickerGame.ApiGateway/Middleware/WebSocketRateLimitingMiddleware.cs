using ClickerGame.ApiGateway.Services;
using System.Security.Claims;

namespace ClickerGame.ApiGateway.Middleware
{
    public class WebSocketRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebSocketRateLimitingMiddleware> _logger;
        private readonly IConfiguration _configuration;

        public WebSocketRateLimitingMiddleware(
            RequestDelegate next,
            ILogger<WebSocketRateLimitingMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if this is a WebSocket request
            if (context.Request.Headers.ContainsKey("Upgrade") &&
                context.Request.Headers["Upgrade"].ToString().Contains("websocket", StringComparison.OrdinalIgnoreCase))
            {
                var webSocketProxyService = context.RequestServices.GetRequiredService<IWebSocketProxyService>();
                var rateLimitService = context.RequestServices.GetRequiredService<IRateLimitService>();

                var clientId = GetClientId(context);
                var endpoint = GetEndpointKey(context);

                // Apply WebSocket-specific rate limiting
                var wsRules = GetWebSocketRules(endpoint);
                foreach (var rule in wsRules)
                {
                    var isAllowed = await rateLimitService.IsRequestAllowedAsync(
                        clientId, $"ws:{endpoint}", rule.Period, rule.Limit);

                    if (!isAllowed)
                    {
                        _logger.LogWarning("WebSocket rate limit exceeded for {ClientId} on {Endpoint}", clientId, endpoint);

                        context.Response.StatusCode = 429;
                        context.Response.Headers["Retry-After"] = GetRetryAfterSeconds(rule.Period).ToString();
                        await context.Response.WriteAsync("WebSocket connection rate limit exceeded");
                        return;
                    }
                }

                // Log WebSocket connection attempt
                await webSocketProxyService.LogWebSocketConnectionAsync(clientId, endpoint, true);

                _logger.LogInformation("WebSocket connection allowed for {ClientId} to {Endpoint}", clientId, endpoint);
            }

            await _next(context);
        }

        private string GetClientId(HttpContext context)
        {
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return $"ip:{ip}";
        }

        private string GetEndpointKey(HttpContext context)
        {
            return context.Request.Path.Value ?? "/";
        }

        private List<WebSocketRateLimitRule> GetWebSocketRules(string endpoint)
        {
            var rules = new List<WebSocketRateLimitRule>();
            var wsRules = _configuration.GetSection("IpRateLimiting:WebSocketRules").Get<WebSocketRateLimitRule[]>() ?? Array.Empty<WebSocketRateLimitRule>();

            foreach (var rule in wsRules)
            {
                if (rule.Endpoint == "*" || endpoint.Contains(rule.Endpoint.Replace("*/", "")))
                {
                    rules.Add(rule);
                }
            }

            return rules;
        }

        private int GetRetryAfterSeconds(string period)
        {
            return period.ToLower() switch
            {
                "1s" => 1,
                "1m" => 60,
                "1h" => 3600,
                "1d" => 86400,
                _ => 60
            };
        }
    }

    public class WebSocketRateLimitRule
    {
        public string Endpoint { get; set; } = "*";
        public string Period { get; set; } = "1h";
        public long Limit { get; set; } = 5;
        public string Description { get; set; } = "";
    }
}