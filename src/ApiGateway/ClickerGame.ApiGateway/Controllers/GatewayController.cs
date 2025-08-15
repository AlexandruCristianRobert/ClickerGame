using Microsoft.AspNetCore.Mvc;

namespace ClickerGame.ApiGateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GatewayController : ControllerBase
    {
        private readonly ILogger<GatewayController> _logger;

        public GatewayController(ILogger<GatewayController> logger)
        {
            _logger = logger;
        }

        [HttpGet("health")]
        public ActionResult GetHealth()
        {
            return Ok(new
            {
                status = "Healthy",
                service = "API Gateway",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }

        [HttpGet("routes")]
        public ActionResult GetRoutes()
        {
            var routes = new
            {
                players = "/api/players/*",
                game = "/api/game/*",
                health = new
                {
                    gateway = "/health",
                    players = "/health/players",
                    gamecore = "/health/gamecore"
                }
            };

            return Ok(routes);
        }
    }
}