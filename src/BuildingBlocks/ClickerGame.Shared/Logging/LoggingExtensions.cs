using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.File;

namespace ClickerGame.Shared.Logging
{
    public static class LoggingExtensions
    {
        public static IServiceCollection AddCorrelationLogging(this IServiceCollection services, string serviceName)
        {
            services.AddScoped<ICorrelationService, CorrelationService>();

            // Configure Serilog with enrichers
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] [{CorrelationId}] {Message:lj} {NewLine}{Exception}")
                .WriteTo.File(
                    path: $"logs/{serviceName}-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ServiceName}] [{CorrelationId}] [{RequestId}] [{UserId}] [{RequestPath}] {Message:lj} {NewLine}{Exception}")
                .CreateLogger();

            return services;
        }

        public static ILogger<T> WithCorrelation<T>(this ILogger<T> logger, ICorrelationService correlationService)
        {
            var context = correlationService.GetContext();

            LogContext.PushProperty("CorrelationId", context.CorrelationId);
            LogContext.PushProperty("RequestId", context.RequestId);
            LogContext.PushProperty("UserId", context.UserId);
            LogContext.PushProperty("UserName", context.UserName);
            LogContext.PushProperty("RequestPath", context.RequestPath);
            LogContext.PushProperty("HttpMethod", context.HttpMethod);
            LogContext.PushProperty("ClientIp", context.ClientIp);

            return logger;
        }

        public static void LogRequestStart<T>(this ILogger<T> logger, ICorrelationService correlationService, string action)
        {
            logger.WithCorrelation(correlationService)
                .LogInformation("Request started: {Action}", action);
        }

        public static void LogRequestEnd<T>(this ILogger<T> logger, ICorrelationService correlationService, string action, long elapsedMs)
        {
            logger.WithCorrelation(correlationService)
                .LogInformation("Request completed: {Action} in {ElapsedMs}ms", action, elapsedMs);
        }

        public static void LogBusinessEvent<T>(this ILogger<T> logger, ICorrelationService correlationService, string eventName, object? data = null)
        {
            logger.WithCorrelation(correlationService)
                .LogInformation("Business event: {EventName} with data: {@Data}", eventName, data);
        }

        public static void LogError<T>(this ILogger<T> logger, ICorrelationService correlationService, Exception exception, string message, params object[] args)
        {
            logger.WithCorrelation(correlationService)
                .LogError(exception, message, args);
        }
    }
}