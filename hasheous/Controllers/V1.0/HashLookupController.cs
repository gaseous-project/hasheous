using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static Authentication.ApiKey;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Endpoints used for looking up hash signatures and their metadata id mappings
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    public class HashLookupController : ControllerBase
    {
        /// <summary>
        /// DEPRECATION Notice - This endpoint was used during development and should not be used anymore. It will be removed at a later date.
        /// Look up the signature coresponding to the provided MD5 and SHA1 hash - and if available any mapped metadata ids
        /// </summary>
        /// <returns>Game and ROM signature from available DATs, and if available mapped metadata ids. 404 if no signature is found.</returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("Lookup")]
        public async Task<IActionResult> Lookup(HashLookupModel model)
        {
            HashLookup hashLookup = new HashLookup(model);

            // send legacy lookups to new lookup code as well - we'll do this until we're sure no one is using this old endpoint anymore
            HashLookup2 hashLookup2 = new HashLookup2(new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString), model);

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