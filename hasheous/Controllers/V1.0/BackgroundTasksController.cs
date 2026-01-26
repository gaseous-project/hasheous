using Microsoft.AspNetCore.Mvc;
using Classes;
using Microsoft.AspNetCore.Authorization;
using Classes.ProcessQueue;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class BackgroundTasksController : Controller
    {
        [HttpGet]
        [MapToApiVersion("1.0")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        public async Task<IActionResult> GetQueue()
        {
            return Ok(await BackgroundTasks.GetServerData());
        }

        [HttpGet]
        [MapToApiVersion("1.0")]
        [Route("{ProcessId:guid}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ManageQueueItem(Guid ProcessId, bool? ForceRun = null, bool? Enabled = null)
        {
            try
            {
                var item = await BackgroundTasks.ManageQueueItem(ProcessId, ForceRun, Enabled);
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

        [HttpPost]
        [MapToApiVersion("1.0")]
        [Route("{ProcessId:guid}/{correlationId:guid}/report")]
        public async Task<IActionResult> ReportProgress(Guid ProcessId, Guid correlationId, [FromBody] Models.ReportModel report)
        {
            // get the background task item
            foreach (var queueItem in QueueProcessor.QueueItems)
            {
                if (queueItem.ProcessId == ProcessId && queueItem.CorrelationId == correlationId.ToString())
                {
                    // update the last report
                    queueItem.LastReport = report;

                    return Ok();
                }
            }

            return Ok();
        }

        [HttpPost]
        [MapToApiVersion("1.0")]
        [Route("Cache/Flush")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult FlushCache()
        {
            if (Config.RedisConfiguration.Enabled == true)
            {
                hasheous.Classes.RedisConnection.PurgeCache();
            }

            return Ok();
        }
    }
}