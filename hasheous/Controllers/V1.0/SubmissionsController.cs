using Authentication;
using Classes;
using Classes.Insights;
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
    [Insight(Insights.InsightSourceType.HashSubmission)]
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

        /// <summary>
        /// Add a vote to a match
        /// </summary>
        /// <param name="model">
        /// The model to add a vote to
        /// </param>
        /// <returns>
        /// The result of the vote
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [Route("FixMatch")]
        [ApiKey()]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> FixMatch(SubmissionsMatchFixModel model)
        {
            Submissions submissions = new Submissions();
            try
            {
                return Ok(await submissions.AddVote(_userManager.GetUserId(HttpContext.User), model));
            }
            catch (HashLookup.HashNotFoundException hnfEx)
            {
                return NotFound("The provided hash was not found in the signature database.");
            }
            catch
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Add an archive observation to the database. An observation is a record of an archive file that has been observed - including its hashes - matching it to a specific ROM hash.
        /// This is used to assit ROM managers in indentifying and managing archive files that contain ROMs.
        /// </summary>
        /// <param name="model">
        /// The model containing the archive observation details
        /// </param>
        /// <returns>
        /// The result of the archive observation submission
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [Route("ArchiveObservation")]
        [ProducesResponseType(typeof(ArchiveObservationModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ApiKey()]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ArchiveObservation(ArchiveObservationModel model)
        {
            Submissions submissions = new Submissions();
            try
            {
                return Ok(await submissions.AddArchiveObservation(_userManager.GetUserId(HttpContext.User), model));
            }
            catch (HashLookup.HashNotFoundException hnfEx)
            {
                return NotFound("The provided content hash was not found in the signature database.");
            }
            catch
            {
                return NotFound();
            }
        }
    }
}