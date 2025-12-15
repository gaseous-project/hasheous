using hasheous_server.Classes;
using hasheous_server.Classes.Tasks.Clients;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Tags Controller
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = false)]
    public class TaskWorkerController : ControllerBase
    {
        #region "Clients"
        /// <summary>
        /// Registers a new task worker client with the specified name and version.
        /// </summary>
        /// <param name="clientName">The name of the client registering as a task worker.</param>
        /// <param name="clientVersion">The version of the client registering as a task worker.</param>
        /// <returns>An IActionResult containing the registration result.</returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authentication.ApiKey.ApiKey()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("clients")]
        public async Task<IActionResult> RegisterClient([FromQuery] string clientName, [FromQuery] string clientVersion)
        {
            // get the api key from the header
            string? apiKey = Request.Headers.TryGetValue(Authentication.ApiKey.ApiKeyHeaderName, out var headerValue) ? headerValue.FirstOrDefault() : null;
            if (apiKey == null)
            {
                return Unauthorized("API key is missing.");
            }

            var result = await ClientManagement.RegisterClient(apiKey, clientName, clientVersion);
            return Ok(result);
        }

        /// <summary>
        /// Unregisters a task worker client using the specified public ID.
        /// </summary>
        /// <param name="publicid">The public identifier of the client to unregister.</param>
        /// <returns>An IActionResult indicating the result of the unregistration.</returns>
        [MapToApiVersion("1.0")]
        [HttpDelete]
        [Authentication.TaskWorkerAPIKey.TaskWorkerAPIKey()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("clients/{publicid}")]
        public async Task<IActionResult> UnregisterClient(string publicid)
        {
            // get the api key from the header
            string? apiKey = Request.Headers.TryGetValue(Authentication.TaskWorkerAPIKey.APIKeyHeaderName, out var headerValue) ? headerValue.FirstOrDefault() : null;
            if (apiKey == null)
            {
                return Unauthorized("API key is missing.");
            }

            var client = await ClientManagement.GetClientByAPIKeyAndPublicId(apiKey, publicid);
            if (client != null)
            {
                await client.Unregister();
            }

            return Ok();
        }

        /// <summary>
        /// Sends a heartbeat signal for a registered task worker client to indicate it is active.
        /// </summary>
        /// <param name="publicid">The public identifier of the client sending the heartbeat.</param>
        /// <returns>An IActionResult indicating the result of the heartbeat operation.</returns>
        [MapToApiVersion("1.0")]
        [HttpPut]
        [Authentication.TaskWorkerAPIKey.TaskWorkerAPIKey()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("clients/{publicid}/heartbeat")]
        public async Task<IActionResult> ClientHeartbeat(string publicid)
        {
            // get the api key from the header
            string? apiKey = Request.Headers.TryGetValue(Authentication.TaskWorkerAPIKey.APIKeyHeaderName, out var headerValue) ? headerValue.FirstOrDefault() : null;
            if (apiKey == null)
            {
                return Unauthorized("API key is missing.");
            }

            var client = await ClientManagement.GetClientByAPIKeyAndPublicId(apiKey, publicid);
            if (client != null)
            {
                await client.Heartbeat();
            }

            return Ok();
        }

        /// <summary>
        /// Retrieves the next job assigned to the specified task worker client.
        /// </summary>
        /// <param name="publicid">The public identifier of the client requesting a job.</param>
        /// <returns>An IActionResult containing the job details or null if no job is available.</returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [Authentication.TaskWorkerAPIKey.TaskWorkerAPIKey()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("clients/{publicid}/job")]
        public async Task<IActionResult> GetJobForClient(string publicid)
        {
            // get the api key from the header
            string? apiKey = Request.Headers.TryGetValue(Authentication.TaskWorkerAPIKey.APIKeyHeaderName, out var headerValue) ? headerValue.FirstOrDefault() : null;
            if (apiKey == null)
            {
                return Unauthorized("API key is missing.");
            }

            // TODO: Implement job retrieval logic here
            // Example placeholder:
            var job = await Task.FromResult<object?>(null); // Replace with actual job fetching logic

            if (job == null)
            {
                return NotFound("No job available for this client.");
            }

            return Ok(job);
        }

        #endregion "Clients"
    }
}