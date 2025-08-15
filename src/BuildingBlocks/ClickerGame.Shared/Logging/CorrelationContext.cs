namespace ClickerGame.Shared.Logging
{
    public class CorrelationContext
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string RequestPath { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public DateTime RequestStartTime { get; set; }
        public string ClientIp { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
    }
}