using Classes;
using Classes.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize(Roles = "Admin")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class RateLimiterController : Controller
    {
        private readonly DynamicRateLimitManager _dynamicRateLimitManager;

        public RateLimiterController(DynamicRateLimitManager dynamicRateLimitManager)
        {
            _dynamicRateLimitManager = dynamicRateLimitManager;
        }

        [HttpGet]
        [MapToApiVersion("1.0")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetRateLimitRules()
        {
            return Ok(_dynamicRateLimitManager.CurrentRules);
        }
    }
}