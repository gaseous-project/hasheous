using Authentication;
using hasheous_server.Classes;
using hasheous_server.Classes.Tasks.Clients;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Tags Controller
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [ApiExplorerSettings(IgnoreApi = false)]
    [IgnoreAntiforgeryToken]
    public class TaskWorkerController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;

        public TaskWorkerController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILoggerFactory loggerFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = loggerFactory.CreateLogger<TaskWorkerController>();
        }

        #region "Clients"
        /// <summary>
        /// Registers a new task worker client with the specified name and version.
        /// </summary>
        /// <param name="clientName">The name of the client registering as a task worker.</param>
        /// <param name="clientVersion">The version of the client registering as a task worker.</param>
        /// <param name="body">Optional body containing additional parameters such as capabilities.</param>
        /// <returns>An IActionResult containing the registration result.</returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authentication.ApiKey.ApiKey()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("clients")]
        public async Task<IActionResult> RegisterClient([FromQuery] string clientName, [FromQuery] string clientVersion, [FromBody] Dictionary<string, object>? body = null)
        {
            // get the api key from the header
            string? apiKey = Request.Headers.TryGetValue(Authentication.ApiKey.ApiKeyHeaderName, out var headerValue) ? headerValue.FirstOrDefault() : null;
            if (apiKey == null)
            {
                return Unauthorized("API key is missing.");
            }

            List<Models.Tasks.Capabilities>? capabilities = null;
            Guid? publicId = null;

            if (body != null)
            {
                if (body.ContainsKey("capabilities"))
                {
                    try
                    {
                        capabilities = System.Text.Json.JsonSerializer.Deserialize<List<Models.Tasks.Capabilities>>(body["capabilities"].ToString() ?? "[]");
                    }
                    catch
                    {
                        capabilities = new List<Models.Tasks.Capabilities>();
                    }
                }
                if (body.ContainsKey("client_id"))
                {
                    if (Guid.TryParse(body["client_id"].ToString(), out var parsedGuid))
                    {
                        publicId = parsedGuid;
                    }
                }
            }

            try
            {
                var result = await ClientManagement.RegisterClient(apiKey, clientName, clientVersion, capabilities, publicId);
                return Ok(result);
            }
            catch
            {
                return BadRequest();
            }
        }

        /// <summary>
        /// Updates the information for a registered task worker client, such as its capabilities.
        /// </summary>
        /// <param name="publicid">The public identifier of the client to update.</param>
        /// <param name="body">A dictionary containing the fields to update, such as "capabilities".</param>
        /// <returns>An IActionResult indicating the result of the update operation.</returns>
        [MapToApiVersion("1.0")]
        [HttpPut]
        [Authentication.TaskWorkerAPIKey.TaskWorkerAPIKey()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("clients/{publicid}")]
        public async Task<IActionResult> UpdateClientInfo(string publicid, [FromBody] Dictionary<string, object> body)
        {
            // get the api key from the header
            string? apiKey = Request.Headers.TryGetValue(Authentication.TaskWorkerAPIKey.APIKeyHeaderName, out var headerValue) ? headerValue.FirstOrDefault() : null;
            if (apiKey == null)
            {
                return Unauthorized("API key is missing or invalid.");
            }
            var client = await ClientManagement.GetClientByAPIKeyAndPublicId(apiKey, publicid);
            if (client == null)
            {
                return NotFound("Client not found.");
            }
            if (body.ContainsKey("capabilities"))
            {
                try
                {
                    var capabilities = System.Text.Json.JsonSerializer.Deserialize<List<Models.Tasks.Capabilities>>(body["capabilities"].ToString() ?? "[]");
                    if (capabilities != null)
                    {
                        client.Capabilities = capabilities;
                        await client.Commit();
                    }
                }
                catch
                {
                    // ignore errors
                }
            }
            return Ok();
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
        /// Retrieves the next job assigned to the specified task worker client. If a job is already assigned, it returns that job.
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

            var job = await ClientManagement.ClientGetTask(apiKey, publicid);

            return Ok(job);
        }

        /// <summary>
        /// Submits the result or status update for a job processed by a task worker client.
        /// </summary>
        /// <param name="publicid">The public identifier of the client submitting the job result.</param>
        /// <param name="body">A dictionary containing required keys: "task_id", "status", and "result", and optional "error_message".</param>
        /// <returns>An IActionResult indicating success or the appropriate error response.</returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authentication.TaskWorkerAPIKey.TaskWorkerAPIKey()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("clients/{publicid}/job")]
        public async Task<IActionResult> SubmitJobResultForClient(string publicid, [FromBody] Dictionary<string, object> body)
        {
            // get the api key from the header
            string? apiKey = Request.Headers.TryGetValue(Authentication.TaskWorkerAPIKey.APIKeyHeaderName, out var headerValue) ? headerValue.FirstOrDefault() : null;
            if (apiKey == null)
            {
                return Unauthorized("API key is missing.");
            }
            if (!body.ContainsKey("task_id") || !body.ContainsKey("status") || !body.ContainsKey("result"))
            {
                return BadRequest("Missing required fields: task_id, status, result.");
            }
            string taskId = body["task_id"].ToString() ?? "";
            if (!Enum.TryParse<Models.Tasks.QueueItemStatus>(body["status"].ToString() ?? "", out var status))
            {
                return BadRequest("Invalid status value.");
            }
            if (status != Models.Tasks.QueueItemStatus.InProgress &&
                status != Models.Tasks.QueueItemStatus.Submitted &&
                status != Models.Tasks.QueueItemStatus.Failed)
            {
                return BadRequest("Status must be one of: InProgress, Submitted, Failed.");
            }
            string result = body["result"].ToString() ?? "";
            string? errorMessage = null;
            if (body.ContainsKey("error_message"))
            {
                errorMessage = body["error_message"].ToString();
            }
            await ClientManagement.ClientSubmitTaskStatusOrResult(apiKey, publicid, taskId, status, result, errorMessage);
            return Ok();
        }

        #endregion "Clients"

        #region "Management"

        /// <summary>
        /// Retrieves all registered task worker clients associated with the current authenticated user.
        /// </summary>
        /// <returns>An IActionResult containing the list of clients for the authenticated user.</returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [Authorize(Roles = "Task Runner")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("user/clients")]
        public async Task<IActionResult> GetClients()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return Forbid();
            }

            List<Models.Tasks.ClientModel> clients = await ClientManagement.GetAllClientsForUserId(user.Id);

            List<Dictionary<string, object?>> result = new List<Dictionary<string, object?>>();

            foreach (var client in clients)
            {
                string? clientStatus = client.GetClientTaskStatus()?.ToString();
                if (clientStatus == null || clientStatus == "")
                {
                    clientStatus = "Inactive";
                }
                Dictionary<string, object?> clientDict = new Dictionary<string, object?>
                {
                    {"client", client },
                    {"taskStatus", clientStatus }
                };
                result.Add(clientDict);
            }
            return Ok(result);
        }

        /// <summary>
        /// Retrieves tasks, optionally filtered by DataObjectId.
        /// </summary>
        /// <param name="DataObjectId">Optional DataObjectId to filter tasks.</param>
        /// <returns>If DataObjectId is null, returns a task progress summary. If DataObjectId is provided, returns tasks filtered by that DataObjectId.</returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("tasks")]
        [Route("tasks/{DataObjectId?}")]
        public async Task<IActionResult> GetTasks(long? DataObjectId = null)
        {
            if (DataObjectId == null)
            {
                var summary = await TaskManagement.GetTaskProgressSummary();
                return Ok(summary);
            }
            else
            {
                var tasks = TaskManagement.GetAllTasks((long)DataObjectId);
                // remove parameters from the tasks before returning
                foreach (var task in tasks)
                {
                    task.Parameters = null;
                }
                return Ok(tasks);
            }
        }

        #endregion "Management"
    }
}