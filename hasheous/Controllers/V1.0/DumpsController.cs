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
    [Authorize]
    public class DumpsController : ControllerBase
    {
        /// <summary>
        /// Returns a zip file containing the metadata map dump.
        /// </summary>
        /// <returns>A zip file containing the metadata map dump.</returns>
        /// <response code="200">Returns the zip file.</response>
        /// <response code="404">If the metadata map dump is not found.</response>
        /// <response code="500">If an error occurs while generating the dump.</response>
        [HttpGet("MetadataMapDump")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMetadataMapDump()
        {
            try
            {
                // Execute the Dumps task to create the metadata map dump
                var dumpsTask = new Dumps();
                await dumpsTask.ExecuteAsync();

                // Define the path to the zip file
                string zipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataMap.zip");

                // Check if the zip file exists
                if (!System.IO.File.Exists(zipFilePath))
                {
                    return NotFound("Metadata map dump not found.");
                }

                // Return the zip file as a file result
                return PhysicalFile(zipFilePath, "application/zip", "MetadataMap.zip");
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