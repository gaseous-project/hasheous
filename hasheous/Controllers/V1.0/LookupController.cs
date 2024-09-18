using Classes;
using hasheous_server.Models;
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
    public class LookupController : ControllerBase
    {
        /// <summary>
        /// Look up the signature coresponding to the provided MD5 and SHA1 hash - and if available, any mapped metadata ids
        /// </summary>
        /// <param name="model">A JSON element with MD5 or SHA1 key value pairs representing the hashes of the ROM being queried.</param>
        /// <returns>Game and ROM signature from available DATs, and if available mapped metadata ids. 404 if no signature is found.</returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("ByHash")]
        public async Task<IActionResult> LookupPost(HashLookupModel model)
        {
            try
            {
                HashLookup hashLookup = new HashLookup(new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString), model);

                if (hashLookup == null)
                {
                    return NotFound();
                }
                else
                {
                    return Ok(hashLookup);
                }
            }
            catch (HashLookup.HashNotFoundException hnfEx)
            {
                return NotFound("The provided hash was not found in the signature database.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("An error occurred while looking up a hash: " + model.MD5 + " " + model.SHA1 + ": " + ex.Message);
                return NotFound();
            }
        }

        /// <summary>
        /// Look up the signature coresponding to the provided MD5 and SHA1 hash - and if available, any mapped metadata ids
        /// </summary>
        /// <param name="md5">An MD5 hash to search for.</param>
        /// <param name="sha1">An SHA1 hash to search for.</param>
        /// <returns>Game and ROM signature from available DATs, and if available mapped metadata ids. 404 if no signature is found.</returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ResponseCache(CacheProfileName = "7Days")]
        [Route("ByHash/md5/{md5}")]
        [Route("ByHash/sha1/{sha1}")]
        public async Task<IActionResult> LookupGet(string? md5, string? sha1)
        {
            try
            {
                HashLookup hashLookup = new HashLookup(new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString), new HashLookupModel
                {
                    MD5 = md5,
                    SHA1 = sha1
                });

                if (hashLookup == null)
                {
                    return NotFound();
                }
                else
                {
                    return Ok(hashLookup);
                }
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
    }
}