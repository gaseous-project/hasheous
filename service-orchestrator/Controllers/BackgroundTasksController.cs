using Microsoft.AspNetCore.Mvc;
using Classes;
using Microsoft.AspNetCore.Authorization;
using static Authentication.InterHostApiKey;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [InterHostApiKey]
    public class BackgroundTasksController : Controller
    {
        [HttpGet]
        [MapToApiVersion("1.0")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [InterHostApiKey]
        public async Task<IActionResult> GetQueue()
        {
            return Ok(await BackgroundTasks.GetServerData(getRemote: false));
        }

        [HttpGet]
        [MapToApiVersion("1.0")]
        [Route("{ProcessId:guid}")]
        [InterHostApiKey]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ManageQueueItem(Guid ProcessId, bool? ForceRun = null, bool? Enabled = null)
        {
            try
            {
                var item = await BackgroundTasks.ManageQueueItem(ProcessId, ForceRun, Enabled, false);
                if (item == null)
                {
                    return NotFound();
                }

                return Ok(item);
            }
            catch (ArgumentException aggEx)
            {
                return BadRequest(aggEx.Message);
            }
            catch (KeyNotFoundException knfEx)
            {
                return NotFound(knfEx.Message);
            }
            catch (Exception ex)
            {
                // Log the exception (not shown here for brevity)
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }
    }
}