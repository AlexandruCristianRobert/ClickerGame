using ClickerGame.Shared.Logging;

namespace ClickerGame.Upgrades.Middleware
{
    public class CorrelationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationMiddleware> _logger;

        public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ICorrelationService correlationService)
        {
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            var requestId = Guid.NewGuid().ToString();

            correlationService.SetCorrelationId(correlationId);
            correlationService.SetServiceName("Upgrades-Service");
            correlationService.SetRequestInfo(
                context.Request.Path,
                context.Request.Method,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                context.Request.Headers["User-Agent"].FirstOrDefault() ?? "unknown"
            );

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst("nameid")?.Value ?? "unknown";
                var userName = context.User.FindFirst("name")?.Value ?? "unknown";
                correlationService.SetUserId(userId, userName);
            }

            context.Response.Headers.Add("X-Correlation-ID", correlationId);
            context.Response.Headers.Add("X-Request-ID", requestId);

            _logger.LogInformation("Request started: {Method} {Path} - CorrelationId: {CorrelationId}",
                context.Request.Method, context.Request.Path, correlationId);

            await _next(context);

            _logger.LogInformation("Request completed: {Method} {Path} - Status: {StatusCode}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode);
        }
    }
}