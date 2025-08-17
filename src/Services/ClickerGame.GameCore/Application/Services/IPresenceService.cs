using ClickerGame.GameCore.Application.DTOs;

namespace ClickerGame.GameCore.Application.Services
{
    public interface IPresenceService
    {
        // Connection Management
        Task AddConnectionAsync(string connectionId, Guid playerId, string username, string? userAgent = null, string? ipAddress = null);
        Task RemoveConnectionAsync(string connectionId);
        Task UpdateConnectionActivityAsync(string connectionId, string activity);

        // Presence Status
        Task<bool> IsPlayerOnlineAsync(Guid playerId);
        Task<PresenceDto?> GetPlayerPresenceAsync(Guid playerId);
        Task<List<PlayerConnectionDto>> GetPlayerConnectionsAsync(Guid playerId);
        Task<int> GetOnlinePlayerCountAsync();
        Task<OnlinePlayersDto> GetOnlinePlayersAsync(int limit = 100);

        // Status Management
        Task SetPlayerStatusAsync(Guid playerId, PresenceStatus status, string? activity = null);
        Task SetPlayerActivityAsync(Guid playerId, string activity);
        Task UpdateLastSeenAsync(Guid playerId);

        // Broadcast Events
        Task BroadcastPresenceUpdateAsync(PresenceUpdateDto update);
        Task BroadcastPlayerJoinedAsync(Guid playerId, string username);
        Task BroadcastPlayerLeftAsync(Guid playerId, string username);
        Task BroadcastOnlineCountAsync(int count);

        // Cleanup and Maintenance
        Task CleanupExpiredPresenceAsync();
        Task<List<Guid>> GetRecentlyDisconnectedPlayersAsync(TimeSpan timespan);
        Task RemovePlayerPresenceAsync(Guid playerId);
    }
}