namespace ClickerGame.ApiGateway.Services
{
    public interface IWebSocketProxyService
    {
        Task<bool> IsWebSocketRequest(HttpContext context);
        Task ProxyWebSocketAsync(HttpContext context, string targetUri);
        Task<WebSocketProxyStats> GetProxyStatsAsync();
        Task LogWebSocketConnectionAsync(string clientId, string targetService, bool isConnected);
    }

    public class WebSocketProxyStats
    {
        public int ActiveConnections { get; set; }
        public int TotalConnections { get; set; }
        public TimeSpan AverageConnectionDuration { get; set; }
        public Dictionary<string, int> ConnectionsByService { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}