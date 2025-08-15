using ClickerGame.Shared.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClickerGame.ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoggingController : ControllerBase
    {
        private readonly ILogger<LoggingController> _logger;
        private readonly ICorrelationService _correlationService;

        public LoggingController(ILogger<LoggingController> logger, ICorrelationService correlationService)
        {
            _logger = logger;
            _correlationService = correlationService;
        }

        [HttpGet("test")]
        public ActionResult TestLogging()
        {
            _logger.LogRequestStart(_correlationService, "TestLogging");

            _logger.LogBusinessEvent(_correlationService, "LogTestEvent", new
            {
                TestData = "This is a test log entry",
                Timestamp = DateTime.UtcNow
            });

            return Ok(new
            {
                message = "Test log entries created",
                correlationId = _correlationService.GetCorrelationId(),
                requestId = _correlationService.GetRequestId(),
                context = _correlationService.GetContext()
            });
        }

        [HttpPost("test-error")]
        public ActionResult TestErrorLogging([FromBody] TestErrorDto dto)
        {
            _logger.LogRequestStart(_correlationService, "TestErrorLogging");

            try
            {
                if (dto.ThrowError)
                {
                    throw new InvalidOperationException($"Test error: {dto.ErrorMessage}");
                }

                _logger.LogBusinessEvent(_correlationService, "TestSuccessEvent", new
                {
                    Message = dto.ErrorMessage
                });

                return Ok(new { message = "No error thrown", data = dto });
            }
            catch (Exception ex)
            {
                _logger.LogError(_correlationService, ex, "Test error occurred with message: {ErrorMessage}", dto.ErrorMessage);
                return StatusCode(500, new { error = "Test error was thrown successfully" });
            }
        }

        [HttpGet("correlation-info")]
        [Authorize]
        public ActionResult GetCorrelationInfo()
        {
            var context = _correlationService.GetContext();

            _logger.LogBusinessEvent(_correlationService, "CorrelationInfoRequested", context);

            return Ok(new
            {
                correlationId = context.CorrelationId,
                requestId = context.RequestId,
                userId = context.UserId,
                userName = context.UserName,
                serviceName = context.ServiceName,
                requestPath = context.RequestPath,
                httpMethod = context.HttpMethod,
                clientIp = context.ClientIp,
                requestStartTime = context.RequestStartTime
            });
        }
    }

    public class TestErrorDto
    {
        public bool ThrowError { get; set; }
        public string ErrorMessage { get; set; } = "Test error message";
    }
}