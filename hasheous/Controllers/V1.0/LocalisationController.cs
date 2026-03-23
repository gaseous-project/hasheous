using hasheous_server.Classes;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Handles localisation queries
    /// </summary>
    [ApiController]
    [Route("localisation")]
    [ApiExplorerSettings(IgnoreApi = false)]
    [ApiVersion("1.0")]
    public class LocalisationController : Controller
    {
        /// <summary>
        /// Gets all available languages and their localisations. The key of the returned dictionary is the language key (e.g. "en", "fr", etc.), and the value is another dictionary containing the language name in English under the "language_name_in_english" key, and the localised language name under the "language_name_localised" key
        /// </summary>
        /// <returns></returns>
        [MapToApiVersion("1.0")]
        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<Dictionary<string, Dictionary<string, string>>> GetAllLocalisations()
        {
            return await Localisation.GetAllLocalisations();
        }

        /// <summary>
        /// Gets the localisation for the specified key
        /// </summary>
        /// <param name="key">The key to get the localisation for</param>
        /// <returns>The localisation for the specified key</returns>
        [MapToApiVersion("1.0")]
        [HttpGet("{key}.json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<Dictionary<string, string>> GetLocalisation(string key)
        {
            return await Localisation.GetLanguageStrings(key);
        }
    }
}