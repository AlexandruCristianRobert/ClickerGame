using ClickerGame.GameCore.Application.Services;
using ClickerGame.Shared.Logging;

namespace ClickerGame.GameCore.Application.Services
{
    public class PresenceCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PresenceCleanupBackgroundService> _logger;
        private readonly ICorrelationService _correlationService;

        public PresenceCleanupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PresenceCleanupBackgroundService> logger,
            ICorrelationService correlationService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _correlationService = correlationService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Presence Cleanup Background Service starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredPresenceAsync();

                    // Run cleanup every 2 minutes
                    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in presence cleanup background service");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task CleanupExpiredPresenceAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();

                await presenceService.CleanupExpiredPresenceAsync();

                _logger.LogDebug("Completed presence cleanup cycle");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during presence cleanup");
            }
        }
    }
}