namespace ClickerGame.Shared.Logging
{
    public class CorrelationService : ICorrelationService
    {
        private readonly CorrelationContext _context;

        public CorrelationService()
        {
            _context = new CorrelationContext
            {
                CorrelationId = Guid.NewGuid().ToString("N")[..8], // Short correlation ID
                RequestId = Guid.NewGuid().ToString("N"),
                RequestStartTime = DateTime.UtcNow
            };
        }

        public string GetCorrelationId() => _context.CorrelationId;
        public string GetRequestId() => _context.RequestId;
        public CorrelationContext GetContext() => _context;

        public void SetCorrelationId(string correlationId)
        {
            if (!string.IsNullOrWhiteSpace(correlationId))
                _context.CorrelationId = correlationId;
        }

        public void SetUserId(string userId, string userName = "")
        {
            _context.UserId = userId ?? string.Empty;
            _context.UserName = userName ?? string.Empty;
        }

        public void SetServiceName(string serviceName)
        {
            _context.ServiceName = serviceName ?? string.Empty;
        }

        public void SetRequestInfo(string path, string method, string clientIp, string userAgent)
        {
            _context.RequestPath = path ?? string.Empty;
            _context.HttpMethod = method ?? string.Empty;
            _context.ClientIp = clientIp ?? string.Empty;
            _context.UserAgent = userAgent ?? string.Empty;
        }
    }
}