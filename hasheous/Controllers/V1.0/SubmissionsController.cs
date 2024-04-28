using Authentication;
using hasheous_server.Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using static Authentication.ApiKey;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Endpoints used for user submissions to the database
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    public class SubmissionsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;

        public SubmissionsController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILoggerFactory loggerFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = loggerFactory.CreateLogger<SubmissionsController>();
        }

        [MapToApiVersion("1.0")]
        [HttpPost]
        [Route("FixMatch")]
        [ApiKey()]
        public async Task<IActionResult> FixMatch(SubmissionsMatchFixModel model)
        {
            Submissions submissions = new Submissions();
            return Ok(submissions.AddVote(_userManager.GetUserId(HttpContext.User), model));
        }






        [MapToApiVersion("1.0")]
        [HttpGet]
        [Route("Test")]
        [ApiKey()]
        public async Task<IActionResult> Test()
        {
            var userStuff = HttpContext.User;
            return Ok("Awesome sauce");
        }
    }
}