using Classes;
using Classes.ProcessQueue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [ApiExplorerSettings(IgnoreApi = false)]
    public class DumpsController : ControllerBase
    {
        /// <summary>
        /// Returns a zip file containing the metadata map dump.
        /// </summary>
        /// <returns>A zip file containing the metadata map dump.</returns>
        /// <response code="200">Returns the zip file.</response>
        /// <response code="404">If the metadata map dump is not found.</response>
        /// <response code="500">If an error occurs while generating the dump.</response>
        [HttpGet("MetadataMap.zip")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMetadataMapDump()
        {
            return await ReturnDumpFile(Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataMap.zip"));
        }

        [HttpGet("platforms")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetAvailablePlatformDumps()
        {
            try
            {
                string platformsDir = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platforms");

                // Check if the platforms directory exists
                if (!Directory.Exists(platformsDir))
                {
                    return NotFound("No platform metadata map dumps found.");
                }

                // Get all zip files in the platforms directory
                var zipFiles = Directory.GetFiles(platformsDir, "*.zip");

                // Get the platform names from the file names
                List<string> platformNames = zipFiles
                    .Select(f => Path.GetFileName(f))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Return the list of platform names
                return Ok(platformNames);
            }
            catch (Exception ex)
            {
                // Log the error and return a 500 status code
                Logging.Log(Logging.LogType.Warning, "DumpsController", "Error retrieving available platform metadata map dumps.", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the available platform metadata map dumps.");
            }
        }

        /// <summary>
        /// Returns a zip file containing the platform specific metadata map dump.
        /// </summary>
        /// <returns>A zip file containing the metadata map dump.</returns>
        /// <response code="200">Returns the zip file.</response>
        /// <response code="404">If the metadata map dump is not found.</response>
        /// <response code="500">If an error occurs while generating the dump.</response>
        [HttpGet("platforms/{platformname}.zip")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPlatformMetadataMapDump(string platformname)
        {
            // Validate input: not null/empty, no path traversal, no invalid filename chars
            if (string.IsNullOrWhiteSpace(platformname) ||
                platformname.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 ||
                platformname.Contains("..") ||
                platformname.Contains("/") ||
                platformname.Contains("\\"))
            {
                return BadRequest("Invalid platform name.");
            }

            return await ReturnDumpFile(Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platforms", $"{platformname}.zip"));
        }

        private async Task<IActionResult> ReturnDumpFile(string zipFilePath)
        {
            try
            {
                // Validate input: not null/empty, no path traversal, no invalid filename chars
                if (string.IsNullOrWhiteSpace(zipFilePath) ||
                    zipFilePath.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 ||
                    zipFilePath.Contains("..") ||
                    zipFilePath.Contains("/") ||
                    zipFilePath.Contains("\\"))
                {
                    return BadRequest("Invalid platform name.");
                }

                // Check if the zip file exists
                if (!System.IO.File.Exists(zipFilePath))
                {
                    return NotFound("Metadata map dump not found.");
                }

                // Prefer PhysicalFileResult so the server can use optimized sendfile/zero-copy where available
                var fileInfo = new System.IO.FileInfo(zipFilePath);

                // Optional: Expose validators to help clients/proxies cache (strong ETag + Last-Modified)
                var lastWrite = fileInfo.LastWriteTimeUtc;
                var etag = '"' + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fileInfo.FullName + "|" + fileInfo.Length + "|" + lastWrite.Ticks))) + '"';
                Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.ETag] = etag;
                Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.LastModified] = lastWrite.ToString("R");
                // Optional: allow long-lived caching if your auth/usage policy permits it
                Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] = "private, max-age=86400"; // 1 day, per-user

                // Conditional GET handling: return 304 if client already has the latest
                var request = Request;
                var clientETag = request.Headers[Microsoft.Net.Http.Headers.HeaderNames.IfNoneMatch].ToString();
                if (!string.IsNullOrEmpty(clientETag) && string.Equals(clientETag, etag, StringComparison.Ordinal))
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
                var ifModifiedSince = request.Headers[Microsoft.Net.Http.Headers.HeaderNames.IfModifiedSince].ToString();
                if (string.IsNullOrEmpty(clientETag) && DateTimeOffset.TryParse(ifModifiedSince, out var ims))
                {
                    // If-Modified-Since has second precision; treat unchanged if not newer
                    if (lastWrite <= ims)
                    {
                        return StatusCode(StatusCodes.Status304NotModified);
                    }
                }

                return PhysicalFile(fileInfo.FullName, "application/zip", fileDownloadName: fileInfo.Name, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                // Log the error and return a 500 status code
                Logging.Log(Logging.LogType.Warning, "DumpsController", "Error generating metadata map dump.", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating the metadata map dump.");
            }
        }
    }
}