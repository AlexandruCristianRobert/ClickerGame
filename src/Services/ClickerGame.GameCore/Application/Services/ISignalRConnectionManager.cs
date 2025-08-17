using ClickerGame.GameCore.Application.DTOs;

namespace ClickerGame.GameCore.Application.Services
{
    public interface ISignalRConnectionManager
    {
        Task AddConnectionAsync(string connectionId, Guid playerId, string username);
        Task RemoveConnectionAsync(string connectionId);
        PlayerConnectionDto? GetConnection(string connectionId);
        Task<IEnumerable<PlayerConnectionDto>> GetPlayerConnectionsAsync(Guid playerId);
        Task<bool> IsPlayerOnlineAsync(Guid playerId);
        Task<int> GetOnlinePlayerCountAsync();
        Task CleanupStaleConnectionsAsync();
        List<PlayerConnectionDto> GetAllConnections();
        Task<List<Guid>> GetOnlinePlayerIdsAsync();
    }

}