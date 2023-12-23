using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiVersion("1.0")]
    public class HashLookupController : ControllerBase
    {
        /// <summary>
        /// Get the current signature counts from the database
        /// </summary>
        /// <returns>Number of sources, publishers, games, and rom signatures in the database</returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Lookup(HashLookupModel model)
        {
            HashLookup hashLookup = new HashLookup(model);

            if (hashLookup == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(hashLookup);
            }
        }
    }
}