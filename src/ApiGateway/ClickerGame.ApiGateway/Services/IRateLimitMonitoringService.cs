namespace ClickerGame.ApiGateway.Services
{
    public interface IRateLimitMonitoringService
    {
        Task<RateLimitStatistics> GetStatisticsAsync();
        Task<List<RateLimitViolation>> GetRecentViolationsAsync(int count = 100);
        Task LogViolationAsync(string clientId, string endpoint, string rule);
    }

    public class RateLimitStatistics
    {
        public int TotalRequests { get; set; }
        public int BlockedRequests { get; set; }
        public double BlockedPercentage { get; set; }
        public Dictionary<string, int> TopBlockedEndpoints { get; set; } = new();
        public Dictionary<string, int> TopBlockedClients { get; set; } = new();
    }

    public class RateLimitViolation
    {
        public DateTime Timestamp { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string Rule { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
    }
}