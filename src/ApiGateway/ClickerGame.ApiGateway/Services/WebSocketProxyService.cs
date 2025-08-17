using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text.Json;

namespace ClickerGame.ApiGateway.Services
{
    public class WebSocketProxyService : IWebSocketProxyService
    {
        private readonly IDatabase _redis;
        private readonly ILogger<WebSocketProxyService> _logger;
        private readonly IConfiguration _configuration;

        private const string StatsKey = "websocket_proxy_stats";
        private const string ConnectionsKey = "websocket_active_connections";

        public WebSocketProxyService(
            IConnectionMultiplexer redis,
            ILogger<WebSocketProxyService> logger,
            IConfiguration configuration)
        {
            _redis = redis.GetDatabase();
            _logger = logger;
            _configuration = configuration;
        }

        public Task<bool> IsWebSocketRequest(HttpContext context)
        {
            return Task.FromResult(
                context.Request.Headers.ContainsKey("Upgrade") &&
                context.Request.Headers["Upgrade"].ToString().Contains("websocket", StringComparison.OrdinalIgnoreCase) &&
                context.Request.Headers.ContainsKey("Connection") &&
                context.Request.Headers["Connection"].ToString().Contains("upgrade", StringComparison.OrdinalIgnoreCase)
            );
        }

        public async Task ProxyWebSocketAsync(HttpContext context, string targetUri)
        {
            try
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                var clientWebSocket = await context.WebSockets.AcceptWebSocketAsync();
                var clientId = GetClientId(context);

                await LogWebSocketConnectionAsync(clientId, targetUri, true);

                using var client = new ClientWebSocket();

                // Copy headers from original request
                CopyHeaders(context.Request.Headers, client.Options);

                var targetUriObj = new Uri(targetUri);
                await client.ConnectAsync(targetUriObj, CancellationToken.None);

                _logger.LogInformation("WebSocket proxy established: {ClientId} -> {TargetUri}", clientId, targetUri);

                // Start bidirectional proxying
                var clientToServer = ProxyDataAsync(clientWebSocket, client, "ClientToServer", clientId);
                var serverToClient = ProxyDataAsync(client, clientWebSocket, "ServerToClient", clientId);

                await Task.WhenAny(clientToServer, serverToClient);

                await LogWebSocketConnectionAsync(clientId, targetUri, false);

                _logger.LogInformation("WebSocket proxy connection closed: {ClientId}", clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebSocket proxy for target {TargetUri}", targetUri);
                throw;
            }
        }

        public async Task<WebSocketProxyStats> GetProxyStatsAsync()
        {
            try
            {
                var statsJson = await _redis.StringGetAsync(StatsKey);
                if (statsJson.HasValue)
                {
                    return JsonSerializer.Deserialize<WebSocketProxyStats>(statsJson!) ?? new WebSocketProxyStats();
                }

                return new WebSocketProxyStats();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving WebSocket proxy stats");
                return new WebSocketProxyStats();
            }
        }

        public async Task LogWebSocketConnectionAsync(string clientId, string targetService, bool isConnected)
        {
            try
            {
                var connectionKey = $"{ConnectionsKey}:{clientId}";

                if (isConnected)
                {
                    var connectionInfo = new
                    {
                        ClientId = clientId,
                        TargetService = targetService,
                        ConnectedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    await _redis.StringSetAsync(connectionKey, JsonSerializer.Serialize(connectionInfo), TimeSpan.FromHours(24));
                    await _redis.HashIncrementAsync($"{StatsKey}:counters", "total_connections");
                    await _redis.HashIncrementAsync($"{StatsKey}:active", targetService);
                }
                else
                {
                    await _redis.KeyDeleteAsync(connectionKey);
                    await _redis.HashDecrementAsync($"{StatsKey}:active", targetService);
                }

                // Update stats
                await UpdateStatsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging WebSocket connection for {ClientId}", clientId);
            }
        }

        private async Task ProxyDataAsync(WebSocket source, WebSocket destination, string direction, string clientId)
        {
            var buffer = new byte[4096];

            try
            {
                while (source.State == WebSocketState.Open && destination.State == WebSocketState.Open)
                {
                    var result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await destination.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    await destination.SendAsync(
                        new ArraySegment<byte>(buffer, 0, result.Count),
                        result.MessageType,
                        result.EndOfMessage,
                        CancellationToken.None);

                    _logger.LogDebug("WebSocket message proxied: {Direction}, Size: {Size}, Client: {ClientId}",
                        direction, result.Count, clientId);
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "WebSocket connection closed during proxy: {Direction}, Client: {ClientId}",
                    direction, clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebSocket proxy data transfer: {Direction}, Client: {ClientId}",
                    direction, clientId);
            }
        }

        private void CopyHeaders(IHeaderDictionary sourceHeaders, ClientWebSocketOptions options)
        {
            foreach (var header in sourceHeaders)
            {
                try
                {
                    // Skip headers that shouldn't be copied
                    if (ShouldSkipHeader(header.Key))
                        continue;

                    // Add headers using the appropriate method
                    options.SetRequestHeader(header.Key, header.Value.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to copy header {HeaderName}", header.Key);
                }
            }
        }

        private static bool ShouldSkipHeader(string headerName)
        {
            var skipHeaders = new[] { "Host", "Connection", "Upgrade", "Sec-WebSocket-Key", "Sec-WebSocket-Version" };
            return skipHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
        }

        private string GetClientId(HttpContext context)
        {
            // Try to get from JWT first
            var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            // Fallback to IP address
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return $"ip:{ip}";
        }

        private async Task UpdateStatsAsync()
        {
            try
            {
                var activeConnections = await _redis.HashGetAllAsync($"{StatsKey}:active");
                var totalConnections = await _redis.HashGetAsync($"{StatsKey}:counters", "total_connections");

                var stats = new WebSocketProxyStats
                {
                    ActiveConnections = activeConnections.Sum(x => (int)x.Value),
                    TotalConnections = totalConnections.HasValue ? (int)totalConnections : 0,
                    ConnectionsByService = activeConnections.ToDictionary(x => x.Name.ToString(), x => (int)x.Value),
                    LastUpdated = DateTime.UtcNow
                };

                await _redis.StringSetAsync(StatsKey, JsonSerializer.Serialize(stats), TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating WebSocket proxy stats");
            }
        }
    }
}