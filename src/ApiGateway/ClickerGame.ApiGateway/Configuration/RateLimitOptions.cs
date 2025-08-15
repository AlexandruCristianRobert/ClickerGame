namespace ClickerGame.ApiGateway.Configuration
{
    public class RateLimitOptions
    {
        public const string SectionName = "RateLimiting";

        public GeneralRules General { get; set; } = new();
        public IpRateLimiting IpRateLimiting { get; set; } = new();
        public ClientRateLimiting ClientRateLimiting { get; set; } = new();
    }

    public class GeneralRules
    {
        public bool EnableEndpointRateLimiting { get; set; } = true;
        public bool StackBlockedRequests { get; set; } = false;
        public string RealIpHeader { get; set; } = "X-Real-IP";
        public string ClientIdHeader { get; set; } = "X-ClientId";
        public int HttpStatusCode { get; set; } = 429;
        public string QuotaExceededResponse { get; set; } = "Rate limit exceeded. Try again later.";
    }

    public class IpRateLimiting
    {
        public bool EnableRateLimiting { get; set; } = true;
        public List<string> IpWhitelist { get; set; } = new();
        public List<string> EndpointWhitelist { get; set; } = new();
        public List<RateLimitRule> GeneralRules { get; set; } = new();
    }

    public class ClientRateLimiting
    {
        public bool EnableRateLimiting { get; set; } = true;
        public List<string> ClientWhitelist { get; set; } = new();
        public List<RateLimitRule> GeneralRules { get; set; } = new();
    }

    public class RateLimitRule
    {
        public string Endpoint { get; set; } = "*";
        public string Period { get; set; } = "1m";
        public long Limit { get; set; } = 100;
    }
}