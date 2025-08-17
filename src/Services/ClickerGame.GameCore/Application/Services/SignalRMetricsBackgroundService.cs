using ClickerGame.GameCore.Application.Services;
using ClickerGame.Shared.Logging;

namespace ClickerGame.GameCore.Application.Services
{
    public class SignalRMetricsBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SignalRMetricsBackgroundService> _logger;

        public SignalRMetricsBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<SignalRMetricsBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SignalR Metrics Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var metricsService = scope.ServiceProvider.GetRequiredService<ISignalRMetricsService>();

                    // Run health checks every 30 seconds
                    await metricsService.CheckConnectionHealthAsync();
                    await metricsService.CheckMessageThroughputAsync();
                    await metricsService.CheckErrorRatesAsync();

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SignalR metrics background service");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait longer on error
                }
            }

            _logger.LogInformation("SignalR Metrics Background Service stopped");
        }
    }
}