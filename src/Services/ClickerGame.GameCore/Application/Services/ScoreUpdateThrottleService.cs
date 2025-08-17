using StackExchange.Redis;
using ClickerGame.Shared.Logging;

namespace ClickerGame.GameCore.Application.Services
{
    public class ScoreUpdateThrottleService : IScoreUpdateThrottleService
    {
        private readonly IDatabase _cache;
        private readonly ILogger<ScoreUpdateThrottleService> _logger;
        private readonly ICorrelationService _correlationService;

        // Throttling configuration - max 1 update per 100ms
        private readonly TimeSpan _throttleWindow = TimeSpan.FromMilliseconds(100);
        private readonly int _maxUpdatesPerSecond = 10; // Additional rate limiting per second

        public ScoreUpdateThrottleService(
            IConnectionMultiplexer redis,
            ILogger<ScoreUpdateThrottleService> logger,
            ICorrelationService correlationService)
        {
            _cache = redis.GetDatabase();
            _logger = logger;
            _correlationService = correlationService;
        }

        public async Task<bool> CanSendScoreUpdateAsync(Guid playerId)
        {
            try
            {
                var throttleKey = $"score_throttle:{playerId}";
                var countKey = $"score_count:{playerId}";

                // Check if enough time has passed since last update (100ms rule)
                var lastUpdateTime = await _cache.StringGetAsync(throttleKey);
                if (lastUpdateTime.HasValue)
                {
                    var lastUpdate = DateTime.FromBinary((long)lastUpdateTime!);
                    var timeSinceLastUpdate = DateTime.UtcNow - lastUpdate;

                    if (timeSinceLastUpdate < _throttleWindow)
                    {
                        return false;
                    }
                }

                // Check updates per second rate limit
                var currentCount = await _cache.StringGetAsync(countKey);
                if (currentCount.HasValue && int.Parse(currentCount!) >= _maxUpdatesPerSecond)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking score update throttle for player {PlayerId}", playerId);
                return true; // Allow update on error to prevent blocking legitimate requests
            }
        }

        public async Task RecordScoreUpdateAsync(Guid playerId)
        {
            try
            {
                var throttleKey = $"score_throttle:{playerId}";
                var countKey = $"score_count:{playerId}";
                var now = DateTime.UtcNow;

                // Record the timestamp of this update
                await _cache.StringSetAsync(throttleKey, now.ToBinary(), _throttleWindow);

                // Increment the per-second counter
                var currentSecond = now.ToString("yyyyMMddHHmmss");
                var secondKey = $"{countKey}:{currentSecond}";

                await _cache.StringIncrementAsync(secondKey);
                await _cache.KeyExpireAsync(secondKey, TimeSpan.FromSeconds(2));

                // Update rolling count
                var count = await _cache.StringIncrementAsync(countKey);
                await _cache.KeyExpireAsync(countKey, TimeSpan.FromSeconds(1));

                _logger.LogDebug("Recorded score update for player {PlayerId}, count: {Count}", playerId, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording score update for player {PlayerId}", playerId);
            }
        }

        public async Task<TimeSpan> GetRemainingThrottleTimeAsync(Guid playerId)
        {
            try
            {
                var throttleKey = $"score_throttle:{playerId}";
                var lastUpdateTime = await _cache.StringGetAsync(throttleKey);

                if (!lastUpdateTime.HasValue)
                {
                    return TimeSpan.Zero;
                }

                var lastUpdate = DateTime.FromBinary((long)lastUpdateTime!);
                var timeSinceLastUpdate = DateTime.UtcNow - lastUpdate;
                var remainingTime = _throttleWindow - timeSinceLastUpdate;

                return remainingTime > TimeSpan.Zero ? remainingTime : TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting remaining throttle time for player {PlayerId}", playerId);
                return TimeSpan.Zero;
            }
        }

        public async Task ClearThrottleAsync(Guid playerId)
        {
            try
            {
                var throttleKey = $"score_throttle:{playerId}";
                var countKey = $"score_count:{playerId}";

                await _cache.KeyDeleteAsync(throttleKey);
                await _cache.KeyDeleteAsync(countKey);

                _logger.LogDebug("Cleared score update throttle for player {PlayerId}", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing throttle for player {PlayerId}", playerId);
            }
        }

        public async Task<ScoreUpdateThrottleInfo> GetThrottleInfoAsync(Guid playerId)
        {
            try
            {
                var throttleKey = $"score_throttle:{playerId}";
                var countKey = $"score_count:{playerId}";

                var lastUpdateTime = await _cache.StringGetAsync(throttleKey);
                var currentCount = await _cache.StringGetAsync(countKey);

                var lastUpdate = DateTime.MinValue;
                if (lastUpdateTime.HasValue)
                {
                    lastUpdate = DateTime.FromBinary((long)lastUpdateTime!);
                }

                var remainingTime = await GetRemainingThrottleTimeAsync(playerId);
                var isThrottled = remainingTime > TimeSpan.Zero;
                var updatesInWindow = currentCount.HasValue ? int.Parse(currentCount!) : 0;

                return new ScoreUpdateThrottleInfo
                {
                    PlayerId = playerId,
                    IsThrottled = isThrottled,
                    LastUpdate = lastUpdate,
                    RemainingThrottleTime = remainingTime,
                    UpdatesInCurrentWindow = updatesInWindow,
                    MaxUpdatesPerWindow = _maxUpdatesPerSecond
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting throttle info for player {PlayerId}", playerId);
                return new ScoreUpdateThrottleInfo { PlayerId = playerId };
            }
        }
    }
}