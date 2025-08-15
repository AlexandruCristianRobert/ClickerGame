using ClickerGame.GameCore.Application.Services;
using ClickerGame.GameCore.Domain.Entities;
using ClickerGame.GameCore.Domain.ValueObjects;
using ClickerGame.GameCore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace ClickerGame.GameCore.Application.Services
{
    public class GameEngineService : IGameEngineService
    {
        private readonly GameCoreDbContext _context;
        private readonly IDatabase _cache;
        private readonly ILogger<GameEngineService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public GameEngineService(
            GameCoreDbContext context,
            IConnectionMultiplexer redis,
            ILogger<GameEngineService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _cache = redis.GetDatabase();
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<BigNumber> ProcessClickAsync(Guid playerId, BigNumber clickPower)
        {
            var session = await GetGameSessionFromCacheOrDb(playerId);
            if (session == null)
            {
                throw new InvalidOperationException("Game session not found");
            }

            try
            {
                var earnedValue = session.ProcessClick(clickPower);

                // Update cache immediately for responsive UI
                await UpdateSessionInCache(session);

                // Periodic database saves (every 10 clicks or 30 seconds)
                if (session.ClickCount % 10 == 0 ||
                    DateTime.UtcNow - session.LastUpdateTime > TimeSpan.FromSeconds(30))
                {
                    await SaveGameSessionAsync(session);
                }

                _logger.LogDebug("Click processed for player {PlayerId}, earned {Value}",
                    playerId, earnedValue);

                return earnedValue;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Click rate limit exceeded for player {PlayerId}: {Message}",
                    playerId, ex.Message);
                throw;
            }
        }

        public async Task<GameSession> GetGameSessionAsync(Guid playerId)
        {
            var session = await GetGameSessionFromCacheOrDb(playerId);
            if (session != null)
            {
                // Calculate offline earnings when player returns
                var offlineEarnings = session.CalculateOfflineEarnings();
                if (offlineEarnings > BigNumber.Zero)
                {
                    await UpdateSessionInCache(session);
                    await SaveGameSessionAsync(session);
                    _logger.LogInformation("Player {PlayerId} earned {Amount} while offline",
                        playerId, offlineEarnings);
                }
            }

            return session ?? throw new InvalidOperationException("Game session not found");
        }

        public async Task<GameSession> CreateGameSessionAsync(Guid playerId, string username)
        {
            var existingSession = await _context.GameSessions
                .FirstOrDefaultAsync(gs => gs.PlayerId == playerId);

            if (existingSession != null)
            {
                existingSession.IsActive = true;
                existingSession.LastUpdateTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await UpdateSessionInCache(existingSession);
                return existingSession;
            }

            var newSession = new GameSession
            {
                SessionId = Guid.NewGuid(),
                PlayerId = playerId,
                PlayerUsername = username,
                Score = BigNumber.Zero,
                ClickCount = 0,
                ClickPower = BigNumber.One,
                PassiveIncomePerSecond = 0,
                StartTime = DateTime.UtcNow,
                LastUpdateTime = DateTime.UtcNow,
                IsActive = true,
                LastAntiCheatCheck = DateTime.UtcNow
            };

            _context.GameSessions.Add(newSession);
            await _context.SaveChangesAsync();
            await UpdateSessionInCache(newSession);

            _logger.LogInformation("Created new game session for player {PlayerId}", playerId);
            return newSession;
        }

        public async Task<BigNumber> CalculateOfflineEarningsAsync(Guid playerId)
        {
            var session = await GetGameSessionAsync(playerId);
            return session.CalculateOfflineEarnings();
        }

        public async Task SaveGameSessionAsync(GameSession session)
        {
            _context.GameSessions.Update(session);
            await _context.SaveChangesAsync();
            await UpdateSessionInCache(session);
        }

        public async Task<bool> ValidatePlayerAsync(Guid playerId, string token)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await httpClient.GetAsync($"http://players-service/api/players/{playerId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate player {PlayerId}", playerId);
                return false;
            }
        }

        private async Task<GameSession?> GetGameSessionFromCacheOrDb(Guid playerId)
        {
            var cacheKey = $"game_session:{playerId}";
            var cachedData = await _cache.StringGetAsync(cacheKey);

            if (cachedData.HasValue)
            {
                return JsonSerializer.Deserialize<GameSession>(cachedData!);
            }

            var session = await _context.GameSessions
                .FirstOrDefaultAsync(gs => gs.PlayerId == playerId && gs.IsActive);

            if (session != null)
            {
                await UpdateSessionInCache(session);
            }

            return session;
        }

        private async Task UpdateSessionInCache(GameSession session)
        {
            var cacheKey = $"game_session:{session.PlayerId}";
            var serializedSession = JsonSerializer.Serialize(session);
            await _cache.StringSetAsync(cacheKey, serializedSession, TimeSpan.FromMinutes(30));
        }
    }
}