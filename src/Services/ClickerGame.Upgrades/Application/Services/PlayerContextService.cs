using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Upgrades.Infrastructure.Data;
using ClickerGame.Shared.Logging;
using Microsoft.EntityFrameworkCore;

namespace ClickerGame.Upgrades.Application.Services
{
    public class PlayerContextService : IPlayerContextService
    {
        private readonly UpgradesDbContext _context;
        private readonly IGameCoreIntegrationService _gameCoreService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PlayerContextService> _logger;
        private readonly ICorrelationService _correlationService;

        public PlayerContextService(
            UpgradesDbContext context,
            IGameCoreIntegrationService gameCoreService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<PlayerContextService> logger,
            ICorrelationService correlationService)
        {
            _context = context;
            _gameCoreService = gameCoreService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _correlationService = correlationService;
        }

        public async Task<PlayerUpgradeContext> GetPlayerContextAsync(Guid playerId)
        {
            // Get player score from GameCore service
            var playerScore = await GetPlayerScoreAsync(playerId);

            // Get player upgrade levels
            var ownedUpgrades = await GetPlayerUpgradeLevelsAsync(playerId);

            // Get game session info for additional context
            var gameSession = await _gameCoreService.GetPlayerGameSessionAsync(playerId);

            // Calculate player level based on upgrade progress
            var playerLevel = CalculatePlayerLevel(ownedUpgrades);
            var clickCount = gameSession?.ClickCount ?? 0;

            return new PlayerUpgradeContext
            {
                PlayerId = playerId,
                CurrentScore = playerScore,
                PlayerLevel = playerLevel,
                ClickCount = new BigNumber(clickCount),
                OwnedUpgrades = ownedUpgrades,
                LastActiveAt = DateTime.UtcNow
            };
        }

        public async Task<BigNumber> GetPlayerScoreAsync(Guid playerId)
        {
            return await _gameCoreService.GetPlayerScoreAsync(playerId);
        }

        public async Task<bool> DeductPlayerScoreAsync(Guid playerId, BigNumber amount)
        {
            return await _gameCoreService.DeductPlayerScoreAsync(playerId, amount, "Upgrade Purchase");
        }

        public async Task<Dictionary<string, int>> GetPlayerUpgradeLevelsAsync(Guid playerId)
        {
            var playerUpgrades = await _context.PlayerUpgrades
                .Where(pu => pu.PlayerId == playerId)
                .ToDictionaryAsync(pu => pu.UpgradeId, pu => pu.Level);

            return playerUpgrades;
        }

        public async Task<bool> ValidatePlayerExistsAsync(Guid playerId)
        {
            try
            {
                // First check if player has a game session
                var hasGameSession = await _gameCoreService.ValidatePlayerSessionAsync(playerId);
                if (!hasGameSession)
                {
                    _logger.LogWarning("Player {PlayerId} does not have an active game session", playerId);
                    return false;
                }

                // Also validate with Players service
                var client = _httpClientFactory.CreateClient();
                var playersUrl = _configuration["Services:Players:BaseUrl"] ?? "http://localhost:5001";

                client.DefaultRequestHeaders.Add("X-Correlation-ID", _correlationService.GetCorrelationId());

                var response = await client.GetAsync($"{playersUrl}/api/players/{playerId}");
                var playerExists = response.IsSuccessStatusCode;

                _logger.LogInformation("Player validation for {PlayerId}: GameSession={GameSession}, PlayerExists={PlayerExists}",
                    playerId, hasGameSession, playerExists);

                return hasGameSession && playerExists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating player existence for {PlayerId}", playerId);
                return false;
            }
        }

        public async Task UpdatePlayerLastActiveAsync(Guid playerId)
        {
            // Update last active timestamp for player upgrades
            var playerUpgrades = await _context.PlayerUpgrades
                .Where(pu => pu.PlayerId == playerId)
                .ToListAsync();

            if (playerUpgrades.Any())
            {
                // Update the most recently upgraded one
                var mostRecent = playerUpgrades.OrderByDescending(pu => pu.LastUpgradedAt).First();
                mostRecent.LastUpgradedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Apply upgrade effects to the player's game session
        /// </summary>
        public async Task<bool> ApplyUpgradeEffectsToGameCoreAsync(Guid playerId, PlayerEffectSummary effects, string sourceUpgradeId)
        {
            try
            {
                var gameCoreEffects = new GameCoreUpgradeEffects
                {
                    ClickPowerBonus = effects.TotalClickPowerBonus.ToDecimal(),
                    PassiveIncomeBonus = effects.TotalPassiveIncomeBonus.ToDecimal(),
                    MultiplierBonus = effects.TotalMultiplier,
                    SourceUpgradeId = sourceUpgradeId
                };

                var success = await _gameCoreService.ApplyUpgradeEffectsAsync(playerId, gameCoreEffects);

                if (success)
                {
                    _logger.LogInformation("Successfully applied upgrade effects to GameCore for player {PlayerId} from upgrade {UpgradeId}",
                        playerId, sourceUpgradeId);
                }
                else
                {
                    _logger.LogWarning("Failed to apply upgrade effects to GameCore for player {PlayerId} from upgrade {UpgradeId}",
                        playerId, sourceUpgradeId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying upgrade effects to GameCore for player {PlayerId}", playerId);
                return false;
            }
        }

        private int CalculatePlayerLevel(Dictionary<string, int> ownedUpgrades)
        {
            var totalLevels = ownedUpgrades.Values.Sum();

            // Simple level calculation: every 10 upgrade levels = 1 player level
            return Math.Max(1, totalLevels / 10);
        }
    }
}