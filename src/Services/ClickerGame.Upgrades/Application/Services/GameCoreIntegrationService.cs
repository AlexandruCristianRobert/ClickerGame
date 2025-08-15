using ClickerGame.Upgrades.Domain.ValueObjects;
using ClickerGame.Shared.Logging;
using System.Text.Json;

namespace ClickerGame.Upgrades.Application.Services
{
    public class GameCoreIntegrationService : IGameCoreIntegrationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GameCoreIntegrationService> _logger;
        private readonly ICorrelationService _correlationService;

        public GameCoreIntegrationService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GameCoreIntegrationService> logger,
            ICorrelationService correlationService)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _correlationService = correlationService;
        }

        public async Task<BigNumber> GetPlayerScoreAsync(Guid playerId)
        {
            try
            {
                var client = CreateHttpClient();
                var gameCoreUrl = GetGameCoreBaseUrl();

                var response = await client.GetAsync($"{gameCoreUrl}/api/game/session/{playerId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var sessionInfo = JsonSerializer.Deserialize<GameSessionResponse>(content, GetJsonOptions());

                    if (sessionInfo?.Score != null)
                    {
                        var score = new BigNumber(decimal.Parse(sessionInfo.Score));
                        _logger.LogInformation("Retrieved player score {Score} for player {PlayerId}", score, playerId);
                        return score;
                    }
                }

                _logger.LogWarning("Failed to get player score from GameCore service for player {PlayerId}. Status: {StatusCode}",
                    playerId, response.StatusCode);

                // Return a fallback value instead of throwing
                return new BigNumber(1000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player score for player {PlayerId}", playerId);
                return new BigNumber(1000); // Fallback value
            }
        }

        public async Task<bool> DeductPlayerScoreAsync(Guid playerId, BigNumber amount, string reason = "Upgrade Purchase")
        {
            try
            {
                var client = CreateHttpClient();
                var gameCoreUrl = GetGameCoreBaseUrl();

                var requestData = new
                {
                    PlayerId = playerId,
                    Amount = amount.ToString(),
                    Reason = reason
                };

                var response = await client.PostAsJsonAsync($"{gameCoreUrl}/api/game/deduct-score", requestData);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully deducted {Amount} from player {PlayerId} for {Reason}",
                        amount, playerId, reason);
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to deduct score from GameCore service for player {PlayerId}. Status: {StatusCode}, Error: {Error}",
                    playerId, response.StatusCode, errorContent);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deducting score for player {PlayerId}, amount {Amount}", playerId, amount);
                return false;
            }
        }

        public async Task<bool> ApplyUpgradeEffectsAsync(Guid playerId, GameCoreUpgradeEffects effects)
        {
            try
            {
                var client = CreateHttpClient();
                var gameCoreUrl = GetGameCoreBaseUrl();

                var requestData = new
                {
                    PlayerId = playerId,
                    ClickPowerBonus = effects.ClickPowerBonus,
                    PassiveIncomeBonus = effects.PassiveIncomeBonus,
                    MultiplierBonus = effects.MultiplierBonus,
                    SourceUpgradeId = effects.SourceUpgradeId
                };

                var response = await client.PostAsJsonAsync($"{gameCoreUrl}/api/game/apply-upgrade-effects", requestData);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully applied upgrade effects for player {PlayerId} from upgrade {UpgradeId}",
                        playerId, effects.SourceUpgradeId);
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to apply upgrade effects for player {PlayerId}. Status: {StatusCode}, Error: {Error}",
                    playerId, response.StatusCode, errorContent);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying upgrade effects for player {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<bool> ValidatePlayerSessionAsync(Guid playerId)
        {
            try
            {
                var client = CreateHttpClient();
                var gameCoreUrl = GetGameCoreBaseUrl();

                var response = await client.GetAsync($"{gameCoreUrl}/api/game/session/{playerId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating player session for {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<GameSessionInfo?> GetPlayerGameSessionAsync(Guid playerId)
        {
            try
            {
                var client = CreateHttpClient();
                var gameCoreUrl = GetGameCoreBaseUrl();

                var response = await client.GetAsync($"{gameCoreUrl}/api/game/session/{playerId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var sessionData = JsonSerializer.Deserialize<GameSessionResponse>(content, GetJsonOptions());

                    if (sessionData != null)
                    {
                        return new GameSessionInfo
                        {
                            SessionId = sessionData.SessionId,
                            PlayerId = sessionData.PlayerId,
                            PlayerUsername = sessionData.PlayerUsername,
                            Score = new BigNumber(decimal.Parse(sessionData.Score)),
                            ClickCount = sessionData.ClickCount,
                            ClickPower = new BigNumber(decimal.Parse(sessionData.ClickPower)),
                            PassiveIncomePerSecond = sessionData.PassiveIncomePerSecond,
                            IsActive = sessionData.IsActive
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting game session for player {PlayerId}", playerId);
                return null;
            }
        }

        private HttpClient CreateHttpClient()
        {
            var client = _httpClientFactory.CreateClient();

            // Add correlation headers
            client.DefaultRequestHeaders.Add("X-Correlation-ID", _correlationService.GetCorrelationId());

            // Set timeout
            client.Timeout = TimeSpan.FromSeconds(30);

            return client;
        }

        private string GetGameCoreBaseUrl()
        {
            return _configuration["Services:GameCore:BaseUrl"] ?? "http://localhost:5002";
        }

        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        // Response DTOs for GameCore API
        private class GameSessionResponse
        {
            public Guid SessionId { get; set; }
            public Guid PlayerId { get; set; }
            public string PlayerUsername { get; set; } = string.Empty;
            public string Score { get; set; } = string.Empty;
            public long ClickCount { get; set; }
            public string ClickPower { get; set; } = string.Empty;
            public decimal PassiveIncomePerSecond { get; set; }
            public bool IsActive { get; set; }
        }
    }
}