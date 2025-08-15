using System.Security.Claims;
using System.Text.Json;
using ClickerGame.ApiGateway.Services;

namespace ClickerGame.ApiGateway.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly IConfiguration _configuration;

        public RateLimitingMiddleware(
            RequestDelegate next,
            ILogger<RateLimitingMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip rate limiting for whitelisted endpoints
            if (IsWhitelistedEndpoint(context.Request.Path, context.Request.Method))
            {
                await _next(context);
                return;
            }

            // Get services from the scoped service provider
            var rateLimitService = context.RequestServices.GetRequiredService<IRateLimitService>();
            var monitoringService = context.RequestServices.GetRequiredService<IRateLimitMonitoringService>();

            var clientId = GetClientId(context);
            var endpoint = GetEndpointKey(context);

            // Check if request should be rate limited
            var rules = GetApplicableRules(endpoint);

            foreach (var rule in rules)
            {
                var isAllowed = await rateLimitService.IsRequestAllowedAsync(
                    clientId, endpoint, rule.Period, rule.Limit);

                if (!isAllowed)
                {
                    await HandleRateLimitExceeded(context, rule, monitoringService, clientId, endpoint);
                    return;
                }
            }

            // Add rate limit headers
            await AddRateLimitHeaders(context, clientId, endpoint, rateLimitService);

            await _next(context);
        }

        private bool IsWhitelistedEndpoint(string path, string method)
        {
            var whitelist = _configuration.GetSection("IpRateLimiting:EndpointWhitelist").Get<string[]>() ?? Array.Empty<string>();
            var endpointKey = $"{method.ToLower()}:{path}";

            return whitelist.Any(w =>
                w.Equals(endpointKey, StringComparison.OrdinalIgnoreCase) ||
                w.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        private string GetClientId(HttpContext context)
        {
            // Try to get authenticated user ID first
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            // Fallback to IP address
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return $"ip:{ip}";
        }

        private string GetEndpointKey(HttpContext context)
        {
            return $"{context.Request.Method.ToLower()}:{context.Request.Path}";
        }

        private List<RateLimitRule> GetApplicableRules(string endpoint)
        {
            var rules = new List<RateLimitRule>();
            var generalRules = _configuration.GetSection("IpRateLimiting:GeneralRules").Get<RateLimitRule[]>() ?? Array.Empty<RateLimitRule>();

            foreach (var rule in generalRules)
            {
                if (rule.Endpoint == "*" || endpoint.Contains(rule.Endpoint.Replace("*/", "")))
                {
                    rules.Add(rule);
                }
            }

            return rules.OrderBy(r => r.Endpoint == "*" ? 1 : 0).ToList(); // Specific rules first
        }

        private async Task HandleRateLimitExceeded(
            HttpContext context,
            RateLimitRule rule,
            IRateLimitMonitoringService monitoringService,
            string clientId,
            string endpoint)
        {
            // Log the violation for monitoring
            await monitoringService.LogViolationAsync(clientId, endpoint, $"{rule.Limit}/{rule.Period}");

            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = GetRetryAfterSeconds(rule.Period).ToString();

            var response = new
            {
                error = "Rate limit exceeded",
                message = $"Too many requests. Limit: {rule.Limit} per {rule.Period}",
                retryAfter = GetRetryAfterSeconds(rule.Period)
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));

            _logger.LogWarning("Rate limit exceeded for {ClientId} on {Endpoint}. Rule: {Limit}/{Period}",
                clientId, endpoint, rule.Limit, rule.Period);
        }

        private async Task AddRateLimitHeaders(
            HttpContext context,
            string clientId,
            string endpoint,
            IRateLimitService rateLimitService)
        {
            try
            {
                var info = await rateLimitService.GetRateLimitInfoAsync(clientId, endpoint);

                context.Response.Headers["X-RateLimit-Limit"] = info.Limit.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, info.Limit - info.CurrentCount).ToString();
                context.Response.Headers["X-RateLimit-Reset"] = new DateTimeOffset(info.ResetTime).ToUnixTimeSeconds().ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding rate limit headers");
            }
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

    public class RateLimitRule
    {
        public string Endpoint { get; set; } = "*";
        public string Period { get; set; } = "1m";
        public long Limit { get; set; } = 100;
    }
}