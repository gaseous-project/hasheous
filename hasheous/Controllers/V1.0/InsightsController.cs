using Classes;
using hasheous_server.Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Authentication;
using Classes.Insights;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ApiVersion("1.0")]
    [Authorize]
    public class InsightsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;

        public InsightsController(
                    UserManager<ApplicationUser> userManager,
                    SignInManager<ApplicationUser> signInManager,
                    IEmailSender emailSender,
                    ILoggerFactory loggerFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = loggerFactory.CreateLogger<AccountController>();
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("app/{Id}/Insights")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInsights(long Id)
        {
            if (Id > 0)
            {
                // get insights for app - requires read permission
                var user = await _userManager.GetUserAsync(User);

                DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

                if (dataObjectPermission.CheckAsync(user, DataObjects.DataObjectType.App, DataObjectPermission.PermissionType.Read, Id).Result)
                {
                    Dictionary<string, object> report = await Insights.GenerateInsightReport(Id);

                    return Ok(report);
                }
                else
                {
                    return NotFound();
                }
            }
            else
            {
                // get insights for the whole site - no permission required
                Dictionary<string, object> report = await Insights.GenerateInsightReport(0);

                return Ok(report);
            }
        }
    }
}