using StackExchange.Redis;
using System.Text.Json;

namespace ClickerGame.ApiGateway.Services
{
    public class RateLimitService : IRateLimitService
    {
        private readonly IDatabase _redis;
        private readonly ILogger<RateLimitService> _logger;

        public RateLimitService(IConnectionMultiplexer redis, ILogger<RateLimitService> logger)
        {
            _redis = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<bool> IsRequestAllowedAsync(string clientId, string endpoint, string period, long limit)
        {
            var key = $"rate_limit:{clientId}:{endpoint}:{period}";
            var windowStart = GetWindowStart(period);
            var windowKey = $"{key}:{windowStart}";

            try
            {
                var current = await _redis.StringIncrementAsync(windowKey);

                if (current == 1)
                {
                    // Set expiration for the window
                    await _redis.KeyExpireAsync(windowKey, GetPeriodTimeSpan(period));
                }

                var isAllowed = current <= limit;

                if (!isAllowed)
                {
                    _logger.LogWarning("Rate limit exceeded for client {ClientId} on endpoint {Endpoint}. Count: {Current}/{Limit}",
                        clientId, endpoint, current, limit);
                }

                return isAllowed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rate limit for client {ClientId}", clientId);
                // Fail open - allow request if Redis is down
                return true;
            }
        }

        public async Task<RateLimitInfo> GetRateLimitInfoAsync(string clientId, string endpoint)
        {
            // For simplicity, using 1-minute window
            var period = "1m";
            var key = $"rate_limit:{clientId}:{endpoint}:{period}";
            var windowStart = GetWindowStart(period);
            var windowKey = $"{key}:{windowStart}";

            try
            {
                var current = await _redis.StringGetAsync(windowKey);
                var currentCount = current.HasValue ? (long)current : 0;

                var ttl = await _redis.KeyTimeToLiveAsync(windowKey);
                var resetTime = DateTime.UtcNow.Add(ttl ?? TimeSpan.Zero);

                return new RateLimitInfo
                {
                    CurrentCount = currentCount,
                    Limit = 100, // Default limit
                    ResetTime = resetTime,
                    RetryAfter = ttl ?? TimeSpan.Zero
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rate limit info for client {ClientId}", clientId);
                return new RateLimitInfo
                {
                    CurrentCount = 0,
                    Limit = 100,
                    ResetTime = DateTime.UtcNow.AddMinutes(1),
                    RetryAfter = TimeSpan.FromMinutes(1)
                };
            }
        }

        public async Task ResetClientLimitsAsync(string clientId)
        {
            try
            {
                var pattern = $"rate_limit:{clientId}:*";
                var keys = await GetKeysAsync(pattern);

                if (keys.Any())
                {
                    await _redis.KeyDeleteAsync(keys.ToArray());
                    _logger.LogInformation("Reset rate limits for client {ClientId}", clientId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting rate limits for client {ClientId}", clientId);
            }
        }

        private long GetWindowStart(string period)
        {
            var now = DateTime.UtcNow;
            return period.ToLower() switch
            {
                "1s" => new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero).ToUnixTimeSeconds(),
                "1m" => new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
                "1h" => new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
                "1d" => new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
                _ => new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).ToUnixTimeSeconds()
            };
        }

        private TimeSpan GetPeriodTimeSpan(string period)
        {
            return period.ToLower() switch
            {
                "1s" => TimeSpan.FromSeconds(1),
                "1m" => TimeSpan.FromMinutes(1),
                "1h" => TimeSpan.FromHours(1),
                "1d" => TimeSpan.FromDays(1),
                _ => TimeSpan.FromMinutes(1)
            };
        }

        private async Task<IEnumerable<RedisKey>> GetKeysAsync(string pattern)
        {
            var keys = new List<RedisKey>();
            await foreach (var key in _redis.Multiplexer.GetServers().First().KeysAsync(pattern: pattern))
            {
                keys.Add(key);
            }
            return keys;
        }
    }
}