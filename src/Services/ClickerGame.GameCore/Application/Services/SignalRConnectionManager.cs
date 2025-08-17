using ClickerGame.GameCore.Application.DTOs;
using ClickerGame.Shared.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ClickerGame.GameCore.Application.Services
{
    public class SignalRConnectionManager : ISignalRConnectionManager
    {
        private readonly IDatabase _cache;
        private readonly IPresenceService _presenceService;
        private readonly ILogger<SignalRConnectionManager> _logger;
        private readonly ConcurrentDictionary<string, PlayerConnectionDto> _connections = new();

        public SignalRConnectionManager(
            IConnectionMultiplexer redis,
            IPresenceService presenceService,
            ILogger<SignalRConnectionManager> logger)
        {
            _cache = redis.GetDatabase();
            _presenceService = presenceService;
            _logger = logger;
        }


        public async Task AddConnectionAsync(string connectionId, Guid playerId, string username)
        {
            try
            {
                var connection = new PlayerConnectionDto
                {
                    ConnectionId = connectionId,
                    PlayerId = playerId,
                    Username = username,
                    ConnectedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _connections.TryAdd(connectionId, connection);

                // Use presence service for comprehensive tracking
                await _presenceService.AddConnectionAsync(connectionId, playerId, username);

                _logger.LogInformation("Added SignalR connection {ConnectionId} for player {PlayerId}", connectionId, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding SignalR connection {ConnectionId}", connectionId);
            }
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            try
            {
                _connections.TryRemove(connectionId, out var connection);

                // Use presence service for comprehensive tracking
                await _presenceService.RemoveConnectionAsync(connectionId);

                if (connection != null)
                {
                    _logger.LogInformation("Removed SignalR connection {ConnectionId} for player {PlayerId}",
                        connectionId, connection.PlayerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing SignalR connection {ConnectionId}", connectionId);
            }
        }

        public PlayerConnectionDto? GetConnection(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connection);
            return connection;
        }

        public async Task<IEnumerable<PlayerConnectionDto>> GetPlayerConnectionsAsync(Guid playerId)
        {
            return await _presenceService.GetPlayerConnectionsAsync(playerId);
        }

        public async Task<bool> IsPlayerOnlineAsync(Guid playerId)
        {
            return await _presenceService.IsPlayerOnlineAsync(playerId);
        }

        public async Task<int> GetOnlinePlayerCountAsync()
        {
            return await _presenceService.GetOnlinePlayerCountAsync();
        }

        public async Task CleanupStaleConnectionsAsync()
        {
            try
            {
                // This would be called by a background service to clean up stale connections
                // Implementation depends on your specific requirements
                _logger.LogInformation("Cleaning up stale SignalR connections");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up stale connections");
            }
        }

        public async Task<List<Guid>> GetOnlinePlayerIdsAsync()
        {
            try
            {
                var onlinePlayers = await _presenceService.GetOnlinePlayersAsync();
                return onlinePlayers.Players.Select(p => p.PlayerId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online player IDs");
                return new List<Guid>();
            }


        }
        public List<PlayerConnectionDto> GetAllConnections()
        {
            return _connections.Values.ToList();
        }
    }
}