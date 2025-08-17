using ClickerGame.GameCore.Application.DTOs;
using ClickerGame.GameCore.Hubs;
using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Text.Json;

namespace ClickerGame.GameCore.Application.Services
{
    public class PresenceService : IPresenceService
    {
        private readonly IDatabase _cache;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILogger<PresenceService> _logger;
        private readonly ICorrelationService _correlationService;

        // Redis key patterns
        private const string PresenceKey = "presence:player:{0}";
        private const string ConnectionKey = "connection:{0}";
        private const string OnlinePlayersKey = "presence:online_players";
        private const string PlayerConnectionsKey = "presence:player_connections:{0}";

        // Presence timeout settings
        private readonly TimeSpan _presenceTimeout = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _connectionTimeout = TimeSpan.FromMinutes(10);

        public PresenceService(
            IConnectionMultiplexer redis,
            IHubContext<GameHub> hubContext,
            ILogger<PresenceService> logger,
            ICorrelationService correlationService)
        {
            _cache = redis.GetDatabase();
            _hubContext = hubContext;
            _logger = logger;
            _correlationService = correlationService;
        }

        #region Connection Management

        public async Task AddConnectionAsync(string connectionId, Guid playerId, string username, string? userAgent = null, string? ipAddress = null)
        {
            try
            {
                var connection = new PlayerConnectionDto
                {
                    ConnectionId = connectionId,
                    PlayerId = playerId,
                    Username = username,
                    ConnectedAt = DateTime.UtcNow,
                    UserAgent = userAgent,
                    IpAddress = ipAddress,
                    IsActive = true
                };

                // Store connection info
                var connectionJson = JsonSerializer.Serialize(connection);
                await _cache.StringSetAsync(string.Format(ConnectionKey, connectionId), connectionJson, _connectionTimeout);

                // Add to player's connections set
                var playerConnectionsKey = string.Format(PlayerConnectionsKey, playerId);
                await _cache.SetAddAsync(playerConnectionsKey, connectionId);
                await _cache.KeyExpireAsync(playerConnectionsKey, _connectionTimeout);

                // Check if this is the first connection for the player
                var isFirstConnection = !await IsPlayerOnlineAsync(playerId);

                // Update or create player presence
                await UpdatePlayerPresenceAsync(playerId, username, PresenceStatus.Online, isFirstConnection);

                // Add to online players set
                await _cache.SetAddAsync(OnlinePlayersKey, playerId.ToString());

                // Broadcast if first connection
                if (isFirstConnection)
                {
                    await BroadcastPlayerJoinedAsync(playerId, username);
                }

                // Update online count
                var onlineCount = await GetOnlinePlayerCountAsync();
                await BroadcastOnlineCountAsync(onlineCount);

                _logger.LogInformation("Added connection {ConnectionId} for player {PlayerId} ({Username})",
                    connectionId, playerId, username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding connection {ConnectionId} for player {PlayerId}", connectionId, playerId);
            }
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            try
            {
                // Get connection info
                var connectionJson = await _cache.StringGetAsync(string.Format(ConnectionKey, connectionId));
                if (!connectionJson.HasValue) return;

                var connection = JsonSerializer.Deserialize<PlayerConnectionDto>(connectionJson!);
                if (connection == null) return;

                var playerId = connection.PlayerId;
                var username = connection.Username;

                // Remove connection
                await _cache.KeyDeleteAsync(string.Format(ConnectionKey, connectionId));

                // Remove from player's connections
                var playerConnectionsKey = string.Format(PlayerConnectionsKey, playerId);
                await _cache.SetRemoveAsync(playerConnectionsKey, connectionId);

                // Check if player has any remaining connections
                var remainingConnections = await _cache.SetLengthAsync(playerConnectionsKey);
                var isLastConnection = remainingConnections == 0;

                if (isLastConnection)
                {
                    // Update presence to offline
                    await UpdatePlayerPresenceAsync(playerId, username, PresenceStatus.Offline, false);

                    // Remove from online players
                    await _cache.SetRemoveAsync(OnlinePlayersKey, playerId.ToString());

                    // Broadcast player left
                    await BroadcastPlayerLeftAsync(playerId, username);
                }

                // Update online count
                var onlineCount = await GetOnlinePlayerCountAsync();
                await BroadcastOnlineCountAsync(onlineCount);

                _logger.LogInformation("Removed connection {ConnectionId} for player {PlayerId}, last connection: {IsLastConnection}",
                    connectionId, playerId, isLastConnection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing connection {ConnectionId}", connectionId);
            }
        }

        public async Task UpdateConnectionActivityAsync(string connectionId, string activity)
        {
            try
            {
                var connectionJson = await _cache.StringGetAsync(string.Format(ConnectionKey, connectionId));
                if (!connectionJson.HasValue) return;

                var connection = JsonSerializer.Deserialize<PlayerConnectionDto>(connectionJson!);
                if (connection == null) return;

                // Update player activity
                await SetPlayerActivityAsync(connection.PlayerId, activity);

                _logger.LogDebug("Updated activity for connection {ConnectionId}: {Activity}", connectionId, activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating connection activity for {ConnectionId}", connectionId);
            }
        }

        #endregion

        #region Presence Status

        public async Task<bool> IsPlayerOnlineAsync(Guid playerId)
        {
            try
            {
                return await _cache.SetContainsAsync(OnlinePlayersKey, playerId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if player {PlayerId} is online", playerId);
                return false;
            }
        }

        public async Task<PresenceDto?> GetPlayerPresenceAsync(Guid playerId)
        {
            try
            {
                var presenceJson = await _cache.StringGetAsync(string.Format(PresenceKey, playerId));
                if (!presenceJson.HasValue) return null;

                return JsonSerializer.Deserialize<PresenceDto>(presenceJson!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting presence for player {PlayerId}", playerId);
                return null;
            }
        }

        public async Task<List<PlayerConnectionDto>> GetPlayerConnectionsAsync(Guid playerId)
        {
            try
            {
                var playerConnectionsKey = string.Format(PlayerConnectionsKey, playerId);
                var connectionIds = await _cache.SetMembersAsync(playerConnectionsKey);

                var connections = new List<PlayerConnectionDto>();
                foreach (var connectionId in connectionIds)
                {
                    var connectionJson = await _cache.StringGetAsync(string.Format(ConnectionKey, connectionId!));
                    if (connectionJson.HasValue)
                    {
                        var connection = JsonSerializer.Deserialize<PlayerConnectionDto>(connectionJson!);
                        if (connection != null)
                        {
                            connections.Add(connection);
                        }
                    }
                }

                return connections;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connections for player {PlayerId}", playerId);
                return new List<PlayerConnectionDto>();
            }
        }

        public async Task<int> GetOnlinePlayerCountAsync()
        {
            try
            {
                return (int)await _cache.SetLengthAsync(OnlinePlayersKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online player count");
                return 0;
            }
        }

        public async Task<OnlinePlayersDto> GetOnlinePlayersAsync(int limit = 100)
        {
            try
            {
                var onlinePlayerIds = await _cache.SetMembersAsync(OnlinePlayersKey);
                var players = new List<PresenceDto>();
                var statusCounts = new Dictionary<PresenceStatus, int>();

                var limitedPlayerIds = onlinePlayerIds.Take(limit);

                foreach (var playerIdValue in limitedPlayerIds)
                {
                    if (Guid.TryParse(playerIdValue!, out var playerId))
                    {
                        var presence = await GetPlayerPresenceAsync(playerId);
                        if (presence != null)
                        {
                            players.Add(presence);

                            // Count statuses
                            statusCounts.TryGetValue(presence.Status, out var count);
                            statusCounts[presence.Status] = count + 1;
                        }
                    }
                }

                return new OnlinePlayersDto
                {
                    TotalOnline = players.Count,
                    Players = players.OrderByDescending(p => p.LastSeen).ToList(),
                    StatusCounts = statusCounts
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting online players");
                return new OnlinePlayersDto();
            }
        }

        #endregion

        #region Status Management

        public async Task SetPlayerStatusAsync(Guid playerId, PresenceStatus status, string? activity = null)
        {
            try
            {
                var presence = await GetPlayerPresenceAsync(playerId);
                if (presence == null) return;

                var previousStatus = presence.Status;
                var updatedPresence = new PresenceDto
                {
                    PlayerId = presence.PlayerId,
                    Username = presence.Username,
                    Status = status,
                    LastSeen = DateTime.UtcNow,
                    ConnectedAt = presence.ConnectedAt,
                    CurrentActivity = activity ?? presence.CurrentActivity,
                    Metadata = presence.Metadata,
                    ConnectionCount = presence.ConnectionCount,
                    UserAgent = presence.UserAgent,
                    IpAddress = presence.IpAddress
                };

                var presenceJson = JsonSerializer.Serialize(updatedPresence);
                await _cache.StringSetAsync(string.Format(PresenceKey, playerId), presenceJson, _presenceTimeout);

                // Broadcast status change if different
                if (status != previousStatus)
                {
                    var update = new PresenceUpdateDto
                    {
                        PlayerId = playerId,
                        Username = presence.Username,
                        Status = status,
                        PreviousStatus = previousStatus,
                        Activity = activity
                    };

                    await BroadcastPresenceUpdateAsync(update);
                }

                _logger.LogDebug("Updated status for player {PlayerId}: {Status}", playerId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting status for player {PlayerId}", playerId);
            }
        }

        public async Task SetPlayerActivityAsync(Guid playerId, string activity)
        {
            try
            {
                var presence = await GetPlayerPresenceAsync(playerId);
                if (presence == null) return;

                var updatedPresence = new PresenceDto
                {
                    PlayerId = presence.PlayerId,
                    Username = presence.Username,
                    Status = presence.Status,
                    LastSeen = DateTime.UtcNow,
                    ConnectedAt = presence.ConnectedAt,
                    CurrentActivity = activity,
                    Metadata = presence.Metadata,
                    ConnectionCount = presence.ConnectionCount,
                    UserAgent = presence.UserAgent,
                    IpAddress = presence.IpAddress
                };

                var presenceJson = JsonSerializer.Serialize(updatedPresence);
                await _cache.StringSetAsync(string.Format(PresenceKey, playerId), presenceJson, _presenceTimeout);

                _logger.LogDebug("Updated activity for player {PlayerId}: {Activity}", playerId, activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting activity for player {PlayerId}", playerId);
            }
        }

        public async Task UpdateLastSeenAsync(Guid playerId)
        {
            try
            {
                var presence = await GetPlayerPresenceAsync(playerId);
                if (presence == null) return;

                var updatedPresence = new PresenceDto
                {
                    PlayerId = presence.PlayerId,
                    Username = presence.Username,
                    Status = presence.Status,
                    LastSeen = DateTime.UtcNow,
                    ConnectedAt = presence.ConnectedAt,
                    CurrentActivity = presence.CurrentActivity,
                    Metadata = presence.Metadata,
                    ConnectionCount = presence.ConnectionCount,
                    UserAgent = presence.UserAgent,
                    IpAddress = presence.IpAddress
                };

                var presenceJson = JsonSerializer.Serialize(updatedPresence);
                await _cache.StringSetAsync(string.Format(PresenceKey, playerId), presenceJson, _presenceTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last seen for player {PlayerId}", playerId);
            }
        }

        #endregion

        #region Broadcast Events

        public async Task BroadcastPresenceUpdateAsync(PresenceUpdateDto update)
        {
            try
            {
                await _hubContext.Clients.Group("GameEvents").SendAsync("PresenceUpdate", update);

                _logger.LogBusinessEvent(_correlationService, "PresenceUpdateBroadcasted", new
                {
                    update.PlayerId,
                    update.Status,
                    update.PreviousStatus
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting presence update for player {PlayerId}", update.PlayerId);
            }
        }

        public async Task BroadcastPlayerJoinedAsync(Guid playerId, string username)
        {
            try
            {
                await _hubContext.Clients.Group("GameEvents").SendAsync("PlayerJoined", new
                {
                    PlayerId = playerId,
                    Username = username,
                    JoinedAt = DateTime.UtcNow
                });

                _logger.LogBusinessEvent(_correlationService, "PlayerJoinedBroadcasted", new
                {
                    PlayerId = playerId,
                    Username = username
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting player joined for {PlayerId}", playerId);
            }
        }

        public async Task BroadcastPlayerLeftAsync(Guid playerId, string username)
        {
            try
            {
                await _hubContext.Clients.Group("GameEvents").SendAsync("PlayerLeft", new
                {
                    PlayerId = playerId,
                    Username = username,
                    LeftAt = DateTime.UtcNow
                });

                _logger.LogBusinessEvent(_correlationService, "PlayerLeftBroadcasted", new
                {
                    PlayerId = playerId,
                    Username = username
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting player left for {PlayerId}", playerId);
            }
        }

        public async Task BroadcastOnlineCountAsync(int count)
        {
            try
            {
                await _hubContext.Clients.Group("GameEvents").SendAsync("OnlinePlayersCount", new
                {
                    Count = count,
                    UpdatedAt = DateTime.UtcNow
                });

                _logger.LogDebug("Broadcasted online count: {Count}", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting online count");
            }
        }

        #endregion

        #region Cleanup and Maintenance

        public async Task CleanupExpiredPresenceAsync()
        {
            try
            {
                var onlinePlayerIds = await _cache.SetMembersAsync(OnlinePlayersKey);
                var expiredPlayers = new List<string>();

                foreach (var playerIdValue in onlinePlayerIds)
                {
                    if (Guid.TryParse(playerIdValue!, out var playerId))
                    {
                        var presence = await GetPlayerPresenceAsync(playerId);
                        if (presence == null || DateTime.UtcNow - presence.LastSeen > _presenceTimeout)
                        {
                            expiredPlayers.Add(playerIdValue!);
                            await RemovePlayerPresenceAsync(playerId);
                        }
                    }
                }

                if (expiredPlayers.Count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired presence records", expiredPlayers.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during presence cleanup");
            }
        }

        public async Task<List<Guid>> GetRecentlyDisconnectedPlayersAsync(TimeSpan timespan)
        {
            try
            {
                // This would require tracking disconnection times
                // For now, return empty list as this requires additional infrastructure
                return new List<Guid>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recently disconnected players");
                return new List<Guid>();
            }
        }

        public async Task RemovePlayerPresenceAsync(Guid playerId)
        {
            try
            {
                // Get presence info before removing
                var presence = await GetPlayerPresenceAsync(playerId);

                // Remove presence record
                await _cache.KeyDeleteAsync(string.Format(PresenceKey, playerId));

                // Remove from online players
                await _cache.SetRemoveAsync(OnlinePlayersKey, playerId.ToString());

                // Clean up connections
                var playerConnectionsKey = string.Format(PlayerConnectionsKey, playerId);
                var connectionIds = await _cache.SetMembersAsync(playerConnectionsKey);

                foreach (var connectionId in connectionIds)
                {
                    await _cache.KeyDeleteAsync(string.Format(ConnectionKey, connectionId!));
                }

                await _cache.KeyDeleteAsync(playerConnectionsKey);

                // Broadcast if was online
                if (presence != null && presence.Status != PresenceStatus.Offline)
                {
                    await BroadcastPlayerLeftAsync(playerId, presence.Username);
                }

                _logger.LogInformation("Removed presence for player {PlayerId}", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing presence for player {PlayerId}", playerId);
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task UpdatePlayerPresenceAsync(Guid playerId, string username, PresenceStatus status, bool isFirstConnection)
        {
            try
            {
                var existingPresence = await GetPlayerPresenceAsync(playerId);
                var connectionCount = 1;

                if (existingPresence != null && !isFirstConnection)
                {
                    connectionCount = existingPresence.ConnectionCount + (status == PresenceStatus.Online ? 1 : -1);
                }

                var presence = new PresenceDto
                {
                    PlayerId = playerId,
                    Username = username,
                    Status = status,
                    LastSeen = DateTime.UtcNow,
                    ConnectedAt = existingPresence?.ConnectedAt ?? DateTime.UtcNow,
                    ConnectionCount = Math.Max(0, connectionCount),
                    CurrentActivity = existingPresence?.CurrentActivity
                };

                var presenceJson = JsonSerializer.Serialize(presence);
                await _cache.StringSetAsync(string.Format(PresenceKey, playerId), presenceJson, _presenceTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player presence for {PlayerId}", playerId);
            }
        }

        #endregion
    }
}