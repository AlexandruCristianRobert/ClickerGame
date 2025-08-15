using StackExchange.Redis;
using System.Text.Json;

namespace ClickerGame.ApiGateway.Services
{
    public class RateLimitMonitoringService : IRateLimitMonitoringService
    {
        private readonly IDatabase _redis;
        private readonly ILogger<RateLimitMonitoringService> _logger;

        public RateLimitMonitoringService(IConnectionMultiplexer redis, ILogger<RateLimitMonitoringService> logger)
        {
            _redis = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<RateLimitStatistics> GetStatisticsAsync()
        {
            try
            {
                var totalRequests = await _redis.StringGetAsync("rate_limit:stats:total_requests");
                var blockedRequests = await _redis.StringGetAsync("rate_limit:stats:blocked_requests");

                var total = totalRequests.HasValue ? (int)totalRequests : 0;
                var blocked = blockedRequests.HasValue ? (int)blockedRequests : 0;

                return new RateLimitStatistics
                {
                    TotalRequests = total,
                    BlockedRequests = blocked,
                    BlockedPercentage = total > 0 ? (double)blocked / total * 100 : 0,
                    TopBlockedEndpoints = await GetTopBlockedAsync("endpoints"),
                    TopBlockedClients = await GetTopBlockedAsync("clients")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rate limit statistics");
                return new RateLimitStatistics();
            }
        }

        public async Task<List<RateLimitViolation>> GetRecentViolationsAsync(int count = 100)
        {
            try
            {
                var violations = await _redis.ListRangeAsync("rate_limit:violations", 0, count - 1);
                return violations.Select(v => JsonSerializer.Deserialize<RateLimitViolation>(v!)).ToList()!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent violations");
                return new List<RateLimitViolation>();
            }
        }

        public async Task LogViolationAsync(string clientId, string endpoint, string rule)
        {
            try
            {
                var violation = new RateLimitViolation
                {
                    Timestamp = DateTime.UtcNow,
                    ClientId = clientId,
                    Endpoint = endpoint,
                    Rule = rule,
                    IpAddress = clientId.StartsWith("ip:") ? clientId.Substring(3) : "unknown"
                };

                var violationJson = JsonSerializer.Serialize(violation);

                // Store violation (keep last 1000)
                await _redis.ListLeftPushAsync("rate_limit:violations", violationJson);
                await _redis.ListTrimAsync("rate_limit:violations", 0, 999);

                // Update statistics
                await _redis.StringIncrementAsync("rate_limit:stats:blocked_requests");
                await _redis.HashIncrementAsync("rate_limit:stats:blocked_endpoints", endpoint);
                await _redis.HashIncrementAsync("rate_limit:stats:blocked_clients", clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging rate limit violation");
            }
        }

        private async Task<Dictionary<string, int>> GetTopBlockedAsync(string type)
        {
            try
            {
                var key = $"rate_limit:stats:blocked_{type}";
                var results = await _redis.HashGetAllAsync(key);

                return results
                    .OrderByDescending(x => (int)x.Value)
                    .Take(10)
                    .ToDictionary(x => x.Name.ToString(), x => (int)x.Value); // Fix: Convert RedisValue to string
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top blocked {Type}", type);
                return new Dictionary<string, int>();
            }
        }
    }
}