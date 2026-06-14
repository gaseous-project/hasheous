using System.Text.Json;
using System.Text.Json.Nodes;
using Classes;
using Classes.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Model Context Protocol (MCP) endpoint hosted by Hasheous.
    /// Supports JSON-RPC methods: initialize, ping, tools/list, tools/call.
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class McpController : ControllerBase
    {
        /// <summary>
        /// Handles MCP JSON-RPC requests.
        /// </summary>
        /// <returns>A JSON-RPC response for request methods and no content for notifications.</returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [HttpPost("/mcp/connect")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Post()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                string rawRequestBody = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(rawRequestBody))
                {
                    return JsonRpc(McpRequestProcessor.BuildErrorResponse(null, -32600, "Invalid Request"));
                }

                JsonNode? jsonNode;
                try
                {
                    jsonNode = JsonNode.Parse(rawRequestBody);
                }
                catch (JsonException)
                {
                    return JsonRpc(McpRequestProcessor.BuildErrorResponse(null, -32700, "Parse error"));
                }

                if (jsonNode is not JsonObject request)
                {
                    return JsonRpc(McpRequestProcessor.BuildErrorResponse(null, -32600, "Invalid Request"));
                }

                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                JsonObject? response = await McpRequestProcessor.ProcessRequestAsync(db, request);
                if (response == null)
                {
                    return NoContent();
                }

                return JsonRpc(response);
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "MCP", $"An error occurred while processing MCP request: {ex.Message}", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the MCP request.");
            }
        }

        [HttpGet("/mcp/connect")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult ConnectInfo()
        {
            string baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            return Ok(new
            {
                name = "Hasheous MCP",
                endpoint = $"{baseUrl}/api/v1/Mcp",
                manifest_url = $"{baseUrl}/.well-known/mcp.json",
                protocol = "MCP",
                transports = new[] { "streamable-http" },
                auth = "none"
            });
        }

        private ContentResult JsonRpc(JsonObject response)
        {
            return Content(response.ToJsonString(), "application/json");
        }
    }
}
