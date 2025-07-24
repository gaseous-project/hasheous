using Classes;
using Classes.Insights;
using hasheous_server.Classes;
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
    [IgnoreAntiforgeryToken]
    [Insight(Insights.InsightSourceType.HashLookup)]
    public class LookupController : ControllerBase
    {
        /// <summary>
        /// Look up the signature coresponding to the provided MD5 and SHA1 hash - and if available, any mapped metadata ids
        /// </summary>
        /// <param name="model" required="true">
        /// A JSON element with MD5 or SHA1 key value pairs representing the hashes of the ROM being queried.
        /// </param>
        /// <param name="returnAllSources" required="false" default="false" example="true">
        /// If true, all sources will be returned. If false, only the first source will be returned.
        /// </param>
        /// <param name="returnSources" required="false" default="null" example="TOSEC, MAMEArcade">
        /// A comma separated list of sources to return. If null, fallback to returnAllSources. Valid options are:
        /// TOSEC, MAMEArcade, MAMEMess, NoIntros, Redump, WHDLoad, RetroAchievements, FBNeo
        /// </param>
        /// <param name="returnFields" required="false" default="All" example="All">
        /// A comma-separated list of fields to return in the response. If "All", all fields will be returned. Valid options are:
        /// All, Publisher, Platform, Signatures, Metadata, Attributes
        /// </param>
        /// <returns>
        /// Game and ROM signature from available DATs, and if available mapped metadata ids. 404 if no signature is found.
        /// </returns>
        /// <response code="200">Returns the game and ROM signature from available DATs, and if available mapped metadata ids.</response>
        /// <response code="404">If no signature is found.</response>
        /// <response code="400">If the provided hash is invalid.</response>
        /// <response code="500">If an error occurs while looking up the hash.</response>
        /// <example>
        /// {
        ///    "MD5": "5d7550788a4d1b47ad81fbbbf5c615a9",
        ///    "SHA1": "274ed5c2ea2ddc855f67d4c4e61c9d9b7eb68403"
        /// }
        /// </example>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [ProducesResponseType(typeof(HashLookup), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ResponseCache(CacheProfileName = "5Minute")]
        [Route("ByHash")]
        public async Task<IActionResult> LookupPost(HashLookupModel model, bool? returnAllSources = false, string? returnFields = "All", string? returnSources = null)
        {
            try
            {
                List<gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom.SignatureSourceType>? returnSourcesList = new List<gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom.SignatureSourceType>();

                if (returnSources != null)
                {
                    string[] sources = returnSources.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (string source in sources)
                    {
                        if (Enum.TryParse<gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom.SignatureSourceType>(source, true, out var sourceType))
                        {
                            returnSourcesList.Add(sourceType);
                        }
                        else
                        {
                            Logging.Log(Logging.LogType.Warning, "Hash Lookup", $"Invalid source type provided: {source}");
                            return BadRequest($"Invalid source type provided: {source}");
                        }
                    }
                }

                HashLookup hashLookup = new HashLookup(new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString), model, returnAllSources, returnFields, returnSourcesList);
                var lookupTask = hashLookup.PerformLookup();
                if (await Task.WhenAny(lookupTask, Task.Delay(TimeSpan.FromSeconds(10))) == lookupTask)
                {
                    // Completed within timeout
                    await lookupTask;
                }
                else
                {
                    // Timed out
                    Response.Headers["Retry-After"] = "90";
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Lookup operation is taking too long, and will continue in the background. Please try again later.");
                }

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
                Logging.Log(Logging.LogType.Warning, "Hash Lookup", "An error occurred while looking up a hash: " + model.MD5 + " " + model.SHA1 + ": " + ex.Message, ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while looking up the hash. Please try again later.");
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
        [ResponseCache(CacheProfileName = "5Minute")]
        [Route("ByHash/md5/{md5}")]
        [Route("ByHash/sha1/{sha1}")]
        [Route("ByHash/sha256/{sha256}")]
        [Route("ByHash/crc/{crc}")]
        public async Task<IActionResult> LookupGet(string? md5, string? sha1, string? sha256, string? crc)
        {
            try
            {
                HashLookup hashLookup = new HashLookup(new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString), new HashLookupModel
                {
                    MD5 = md5,
                    SHA1 = sha1,
                    SHA256 = sha256,
                    CRC = crc
                });
                await hashLookup.PerformLookup();

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

        /// <summary>
        /// Get a list of all available platforms in the database
        /// </summary>
        /// <returns>A list of all available platforms in the database</returns>
        /// <response code="200">Returns a list of all available platforms in the database</response>
        /// <response code="404">If no platforms are found in the database</response>
        /// <response code="400">If the page number or page size values are invalid</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ResponseCache(CacheProfileName = "7Days")]
        [Route("Platforms")]
        public async Task<IActionResult> Platforms(int PageNumber = 1, int PageSize = 100)
        {
            if (PageSize > 1000)
            {
                return BadRequest("PageSize must be less than or equal to 1000.");
            }

            if (PageNumber < 1)
            {
                return BadRequest("PageNumber must be greater than or equal to 1.");
            }

            try
            {
                DataObjects dataObjects = new DataObjects();
                DataObjectsList platforms = await dataObjects.GetDataObjects(DataObjects.DataObjectType.Platform, PageNumber, PageSize);

                if (platforms.Count == 0)
                {
                    return NotFound();
                }
                else
                {
                    return Ok(platforms);
                }
            }
            catch
            {
                return NotFound();
            }
        }
    }
}