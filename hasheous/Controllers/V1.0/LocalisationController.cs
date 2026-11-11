using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Authentication;
using Classes;
using Classes.Insights;
using hasheous_server.Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("localisation/")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    public class LocalisationController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;

        public LocalisationController(
                    UserManager<ApplicationUser> userManager,
                    SignInManager<ApplicationUser> signInManager,
                    IEmailSender emailSender,
                    ILoggerFactory loggerFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = loggerFactory.CreateLogger<LocalisationController>();
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("languages.json")]
        public async Task<IActionResult> GetLanguages()
        {
            List<LocalisationItem> languages = await Localisation.GetLanguages();
            return Ok(languages);
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("request/{locale}.json")]
        public async Task<IActionResult> RequestLocalisation(string locale)
        {
            // generate a task to generate the requested localisation and variants using an AI task

            string language = locale.Split('-')[0];

            // get all available localisations for the language from cultureinfo
            var cultures = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
                .Where(c => c.TwoLetterISOLanguageName == language)
                .ToList();

            // generate the requested task for the localisation using the task manager
            // check for a dataobject related to the localisation
            var dataObjects = new DataObjects();
            var localisationDataObject = await dataObjects.SearchDataObject(DataObjects.DataObjectType.Language, language, null);

            hasheous_server.Models.DataObjectItem dataObjectItem;
            if (localisationDataObject == null)
            {
                // object does not exist, generate a new localisation data object
                dataObjectItem = await dataObjects.NewDataObject(DataObjects.DataObjectType.Language, new DataObjectItemModel
                {
                    IsBlockedFromMatching = true,
                    Name = language
                });
            }
            else
            {
                dataObjectItem = localisationDataObject;
            }

            // check if the data object has a localisation task already queued - queue if not, otherwise do nothing
            var tasks = hasheous_server.Classes.Tasks.Clients.TaskManagement.GetAllTasks(dataObjectItem.Id);
            if (tasks.Find(t => t.TaskName == Models.Tasks.TaskType.AILocalisationGeneration) == null)
            {
                // queue a task to generate the localisation for this new data object
                hasheous_server.Classes.Tasks.Clients.TaskManagement.EnqueueTask(dataObjectItem.Id, Models.Tasks.TaskType.AILocalisationGeneration, new List<Models.Tasks.Capabilities> { Models.Tasks.Capabilities.Internet, Models.Tasks.Capabilities.AI, Models.Tasks.Capabilities.DiskSpace }, new Dictionary<string, string> { { "language", language } });
            }

            return Ok();
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("{locale}.json")]
        public async Task<IActionResult> GetLocalisation(string locale)
        {
            // ensure locale is valid since we'll likely be hitting the disk
            string localePattern = @"^[a-z]{2}(?:-[A-Z]{2}|_[A-Z]{2})?$";
            if (!Regex.IsMatch(locale, localePattern))
            {
                return NotFound();
            }

            // try to load the requested localisation item
            try
            {
                LocalisationItem? item = await Localisation.GetLocalisationItem(locale);
                if (item != null)
                {
                    // merge missing translations from the English localisation item
                    if (locale == "en")
                    {
                        return Ok(item);
                    }
                    else
                    {
                        return Ok(item);
                    }
                }
                return NotFound();
            }
            catch (Exception ex)
            {
                // Log the exception if necessary
                return NotFound(ex.Message);
            }
        }
    }
}