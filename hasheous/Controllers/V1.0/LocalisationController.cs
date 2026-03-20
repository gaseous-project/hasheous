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