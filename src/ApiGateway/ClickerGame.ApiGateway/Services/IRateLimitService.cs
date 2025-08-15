namespace ClickerGame.ApiGateway.Services
{
    public interface IRateLimitService
    {
        Task<bool> IsRequestAllowedAsync(string clientId, string endpoint, string period, long limit);
        Task<RateLimitInfo> GetRateLimitInfoAsync(string clientId, string endpoint);
        Task ResetClientLimitsAsync(string clientId);
    }

    public class RateLimitInfo
    {
        public long CurrentCount { get; set; }
        public long Limit { get; set; }
        public DateTime ResetTime { get; set; }
        public TimeSpan RetryAfter { get; set; }
    }
}