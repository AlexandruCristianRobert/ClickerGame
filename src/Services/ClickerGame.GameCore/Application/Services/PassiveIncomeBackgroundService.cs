using ClickerGame.GameCore.Application.Services;
using ClickerGame.GameCore.Infrastructure.Data;
using ClickerGame.Shared.Logging;
using Microsoft.EntityFrameworkCore;

namespace ClickerGame.GameCore.Application.Services
{
    public class PassiveIncomeBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PassiveIncomeBackgroundService> _logger;
        private readonly ICorrelationService _correlationService;

        public PassiveIncomeBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PassiveIncomeBackgroundService> logger,
            ICorrelationService correlationService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _correlationService = correlationService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Passive Income Background Service starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAllActivePlayersPassiveIncomeAsync();

                    // Process every 30 seconds
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in passive income background service");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task ProcessAllActivePlayersPassiveIncomeAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<GameCoreDbContext>();
                var gameEngine = scope.ServiceProvider.GetRequiredService<IGameEngineService>();

                // Get all active players with passive income
                var activePlayers = await context.GameSessions
                    .Where(gs => gs.IsActive &&
                                gs.PassiveIncomePerSecond > 0 &&
                                gs.LastUpdateTime < DateTime.UtcNow.AddMinutes(-1))
                    .Select(gs => gs.PlayerId)
                    .ToListAsync();

                var processedCount = 0;
                foreach (var playerId in activePlayers)
                {
                    try
                    {
                        var earnings = await gameEngine.ProcessPassiveIncomeAsync(playerId);
                        if (earnings > Domain.ValueObjects.BigNumber.Zero)
                        {
                            processedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing passive income for player {PlayerId}", playerId);
                    }
                }

                if (processedCount > 0)
                {
                    _logger.LogDebug("Processed passive income for {Count} players", processedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing passive income for all players");
            }
        }
    }
}