using ClickerGame.GameCore.Application.DTOs.Notifications;
using ClickerGame.GameCore.Application.Services;
using StackExchange.Redis;
using System.Text.Json;

namespace ClickerGame.GameCore.Application.Services
{
    public class ScheduledEventBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduledEventBackgroundService> _logger;
        private readonly IDatabase _cache;

        public ScheduledEventBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ScheduledEventBackgroundService> logger,
            IConnectionMultiplexer redis)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cache = redis.GetDatabase();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled Event Background Service starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessScheduledEvents();
                    await ProcessEventCountdowns();
                    await ProcessGoldenCookieSpawning();

                    // Check every 30 seconds
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in scheduled event background service");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task ProcessScheduledEvents()
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                var key = $"scheduled_events:{currentTime:yyyyMMddHHmm}";

                var scheduledEvents = await _cache.ListRangeAsync(key);

                if (scheduledEvents.Length > 0)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var systemEventService = scope.ServiceProvider.GetRequiredService<ISystemEventService>();

                    foreach (var eventData in scheduledEvents)
                    {
                        if (eventData.HasValue)
                        {
                            try
                            {
                                // Process scheduled events
                                _logger.LogInformation("Processing scheduled event at {CurrentTime}", currentTime);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing scheduled event");
                            }
                        }
                    }

                    // Clean up processed events
                    await _cache.KeyDeleteAsync(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled events");
            }
        }

        private async Task ProcessEventCountdowns()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var systemEventService = scope.ServiceProvider.GetRequiredService<ISystemEventService>();

                // Process active countdowns
                var server = _cache.Multiplexer.GetServer(_cache.Multiplexer.GetEndPoints().First());
                var keys = server.Keys(pattern: "event_countdown:*");

                foreach (var key in keys)
                {
                    var eventData = await _cache.HashGetAllAsync(key);
                    if (eventData.Length > 0)
                    {
                        var eventInfo = eventData.ToDictionary(x => x.Name!, x => x.Value!);
                        var eventId = eventInfo["eventId"];
                        var startTime = DateTime.Parse(eventInfo["startTime"]);
                        var endTime = DateTime.Parse(eventInfo["endTime"]);
                        var isActive = bool.Parse(eventInfo.GetValueOrDefault("isActive", "false"));

                        var now = DateTime.UtcNow;

                        if (now < startTime && isActive)
                        {
                            var timeRemaining = startTime - now;
                            await systemEventService.UpdateEventCountdownAsync(eventId, timeRemaining);

                            if (timeRemaining.TotalSeconds <= 0)
                            {
                                await systemEventService.BroadcastEventStartedAsync(eventId, eventInfo.GetValueOrDefault("description", eventId));
                            }
                        }
                        else if (now >= startTime && now <= endTime && isActive)
                        {
                            var timeRemaining = endTime - now;

                            if (timeRemaining.TotalSeconds <= 0)
                            {
                                await systemEventService.BroadcastEventEndedAsync(eventId, eventInfo.GetValueOrDefault("description", eventId));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event countdowns");
            }
        }

        private async Task ProcessGoldenCookieSpawning()
        {
            try
            {
                // Random golden cookie spawning logic
                var random = new Random();
                if (random.NextDouble() < 0.05) // 5% chance every 30 seconds
                {
                    using var scope = _serviceProvider.CreateScope();
                    var systemEventService = scope.ServiceProvider.GetRequiredService<ISystemEventService>();

                    var goldenCookie = new GoldenCookieNotificationDto
                    {
                        CookieId = Guid.NewGuid().ToString(),
                        Title = "🍪 Golden Cookie Appeared!",
                        Message = "A golden cookie has appeared! Click it quickly for bonuses!",
                        CookieType = (GoldenCookieType)random.Next(1, 8),
                        MultiplierBonus = 1.0m + (decimal)(random.NextDouble() * 4), // 1x to 5x multiplier
                        ClickPowerBonus = (random.Next(10, 100)).ToString(),
                        IsRare = random.NextDouble() < 0.1, // 10% chance for rare
                        AvailableDuration = TimeSpan.FromSeconds(random.Next(10, 30)),
                        ExpiresAt = DateTime.UtcNow.AddSeconds(random.Next(10, 30))
                    };

                    await systemEventService.SpawnGoldenCookieAsync(goldenCookie);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing golden cookie spawning");
            }
        }
    }
}