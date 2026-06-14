using System.Reflection;
using System.Text.Json.Nodes;
using Classes.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class WellKnownController : ControllerBase
    {
        [HttpGet("/.well-known/mcp.json")]
        [Produces("application/json")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public IActionResult GetMcpManifest()
        {
            string baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            string endpointUrl = $"{baseUrl}/api/v1/Mcp";
            string manifestUrl = $"{baseUrl}/.well-known/mcp.json";
            string documentationUrl = "https://github.com/gaseous-project/hasheous/blob/main/README-MCP.MD";
            string openApiUrl = $"{baseUrl}/swagger/v1/swagger.json";
            string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

            JsonArray tools = new JsonArray();
            foreach (McpRequestProcessor.McpToolDescriptor tool in McpRequestProcessor.ToolDescriptors)
            {
                tools.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description
                });
            }

            JsonObject manifest = new JsonObject
            {
                ["name"] = "Hasheous",
                ["description"] = "Lookup video game signature, ROM hash, and game metadata from the Hasheous database over MCP.",
                ["version"] = version,
                ["protocol"] = "MCP",
                ["protocolVersion"] = "2024-11-05",
                ["endpoint"] = endpointUrl,
                ["manifest_url"] = manifestUrl,
                ["docs"] = documentationUrl,
                ["transports"] = new JsonArray("streamable-http"),
                ["auth"] = "none",
                ["methods"] = new JsonArray("POST"),
                ["tools"] = tools,
                ["openapi_spec"] = openApiUrl
            };

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Ok(manifest);
        }
    }
}
