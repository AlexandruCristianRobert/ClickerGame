using ClickerGame.Shared.Logging;
using Serilog.Context;
using System.Security.Claims;

namespace ClickerGame.GameCore.Middleware
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

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationService = context.RequestServices.GetRequiredService<ICorrelationService>();

            // Get correlation ID from header (passed from API Gateway)
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                              ?? correlationService.GetCorrelationId();

            correlationService.SetCorrelationId(correlationId);
            correlationService.SetServiceName("GameCore-Service");

            // Set request information
            correlationService.SetRequestInfo(
                context.Request.Path,
                context.Request.Method,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                context.Request.Headers["User-Agent"].FirstOrDefault() ?? "unknown"
            );

            // Set user information if authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
                var userName = context.User.FindFirst(ClaimTypes.Name)?.Value ?? "";
                correlationService.SetUserId(userId, userName);
            }

            // Add correlation ID to response headers
            context.Response.Headers["X-Correlation-ID"] = correlationId;

            // Set up logging context
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("RequestId", correlationService.GetRequestId()))
            using (LogContext.PushProperty("ServiceName", "GameCore-Service"))
            using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
            using (LogContext.PushProperty("HttpMethod", context.Request.Method))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                _logger.LogInformation("Request started: {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                try
                {
                    await _next(context);
                }
                finally
                {
                    stopwatch.Stop();
                    _logger.LogInformation("Request completed: {Method} {Path} with status {StatusCode} in {ElapsedMs}ms",
                        context.Request.Method, context.Request.Path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}