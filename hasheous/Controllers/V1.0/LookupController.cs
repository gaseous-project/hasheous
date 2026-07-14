using System.Text.Json;
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
        private const string zeroByteMD5 = "d41d8cd98f00b204e9800998ecf8427e";
        private const string zeroByteSHA1 = "da39a3ee5e6b4b0d3255bfef95601890afd80709";
        private const string zeroByteSHA256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        private const string zeroByteCRC = "00000000";
        private const int MaxLookupPayloadBytes = 262_144;
        private const int MaxLookupArrayItems = 50;

        private static readonly JsonSerializerOptions HashLookupJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Look up the signature coresponding to the provided MD5 and SHA1 hash - and if available, any mapped metadata ids
        /// </summary>
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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ResponseCache(CacheProfileName = "5Minute")]
        [RequestSizeLimit(MaxLookupPayloadBytes)]
        [Route("ByHash")]
        public async Task<IActionResult> LookupPost(bool? returnAllSources = false, string? returnFields = "All", string? returnSources = null)
        {
            try
            {
                List<gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom.SignatureSourceType> returnSourcesList = new List<gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom.SignatureSourceType>();

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

                if (Request.ContentLength.HasValue && Request.ContentLength.Value > MaxLookupPayloadBytes)
                {
                    return StatusCode(StatusCodes.Status413PayloadTooLarge, $"Payload exceeds the maximum allowed size of {MaxLookupPayloadBytes / 1024}KB.");
                }

                // [{"crc": "12ec7f82"}, {"crc": "836a0187"}]
                List<hasheous_server.Models.HashLookupModel> modelList = new List<hasheous_server.Models.HashLookupModel>();
                JsonElement normalizedModel;

                if (Request.Body == null || (Request.ContentLength.HasValue && Request.ContentLength.Value == 0))
                {
                    return BadRequest("Invalid model payload. Provide a JSON object or array containing hash fields.");
                }

                try
                {
                    using JsonDocument parsedBody = await JsonDocument.ParseAsync(Request.Body);
                    normalizedModel = parsedBody.RootElement.Clone();
                }
                catch (JsonException)
                {
                    return BadRequest("Invalid model payload. Body must be valid JSON.");
                }

                // Some clients send JSON text as a quoted string (for example, "[{...}]").
                // Parse the inner JSON so both raw and string-encoded JSON are accepted.
                if (normalizedModel.ValueKind == JsonValueKind.String)
                {
                    string? rawModelString = normalizedModel.GetString();
                    if (string.IsNullOrWhiteSpace(rawModelString))
                    {
                        return BadRequest("Invalid model payload. Provide a JSON object or array containing hash fields.");
                    }

                    try
                    {
                        using JsonDocument parsedStringModel = JsonDocument.Parse(rawModelString);
                        normalizedModel = parsedStringModel.RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        return BadRequest("Invalid model payload. String body must contain valid JSON object or array text.");
                    }
                }

                // Accept raw JSON object or array in request body and deserialize manually.
                try
                {
                    if (normalizedModel.ValueKind == JsonValueKind.Array)
                    {
                        modelList = JsonSerializer.Deserialize<List<hasheous_server.Models.HashLookupModel>>(normalizedModel, HashLookupJsonOptions) ?? new List<hasheous_server.Models.HashLookupModel>();
                        if (modelList.Count > MaxLookupArrayItems)
                        {
                            return BadRequest($"Invalid model payload. A maximum of {MaxLookupArrayItems} hash items is allowed.");
                        }
                    }
                    else if (normalizedModel.ValueKind == JsonValueKind.Object)
                    {
                        var deserializedModel = JsonSerializer.Deserialize<hasheous_server.Models.HashLookupModel>(normalizedModel, HashLookupJsonOptions);
                        if (deserializedModel != null)
                        {
                            modelList.Add(deserializedModel);
                        }
                    }
                    else
                    {
                        return BadRequest("Invalid model payload. Provide a JSON object or array containing hash fields.");
                    }
                }
                catch (JsonException)
                {
                    return BadRequest("Invalid model payload. Unable to deserialize request body into hash lookup model(s).");
                }

                // Drop known zero-byte hashes before lookup to avoid unnecessary work.
                modelList = modelList.Where(x => !IsKnownZeroByteHash(x)).ToList();

                if (modelList.Count == 0)
                {
                    return BadRequest("Invalid model payload. No valid hash items remain after removing zero-byte hashes.");
                }

                if (modelList.Count == 0 || modelList.All(x => string.IsNullOrWhiteSpace(x.MD5) && string.IsNullOrWhiteSpace(x.SHA1) && string.IsNullOrWhiteSpace(x.SHA256) && string.IsNullOrWhiteSpace(x.CRC)))
                {
                    return BadRequest("Invalid model payload. Provide at least one hash field (MD5, SHA1, SHA256, CRC).");
                }

                HashLookup hashLookup = new HashLookup(new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString), modelList, returnAllSources, returnFields, returnSourcesList);
                await hashLookup.PerformLookup(true);

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
                return NotFound(hnfEx.Message);
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "Hash Lookup", "An error occurred while looking up a hash: " + ex.Message, ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while looking up the hash. Please try again later.");
            }
        }

        private static bool IsKnownZeroByteHash(HashLookupModel model)
        {
            return IsMatch(model.MD5, zeroByteMD5) ||
                   IsMatch(model.SHA1, zeroByteSHA1) ||
                   IsMatch(model.SHA256, zeroByteSHA256) ||
                   IsMatch(model.CRC, zeroByteCRC);
        }

        private static bool IsMatch(string? input, string knownValue)
        {
            return !string.IsNullOrWhiteSpace(input) &&
                   string.Equals(input.Trim(), knownValue, StringComparison.OrdinalIgnoreCase);
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
            // fail on obvious zero byte hashes to avoid unnecessary lookups
            if (md5 == zeroByteMD5 || sha1 == zeroByteSHA1 || sha256 == zeroByteSHA256 || crc == zeroByteCRC)
            {
                return BadRequest("Invalid hash provided. Zero-byte hashes are not allowed.");
            }

            try
            {
                HashLookup hashLookup = new HashLookup(new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString), new List<hasheous_server.Models.HashLookupModel>
                {
                    new hasheous_server.Models.HashLookupModel
                    {
                        MD5 = md5,
                        SHA1 = sha1,
                        SHA256 = sha256,
                        CRC = crc
                    }
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
            catch (HashLookup.HashNotFoundException)
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