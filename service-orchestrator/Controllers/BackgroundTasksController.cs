using Classes;
using Classes.ProcessQueue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
                    // Calculate speed and ETA for each progress item
                    if (report?.Progress != null)
                    {
                        var currentTime = DateTime.UtcNow;

                        foreach (var progressKey in report.Progress.Keys.ToList())
                        {
                            var progressItem = report.Progress[progressKey];

                            // Only calculate if count and total are valid
                            if (progressItem.count.HasValue && progressItem.total.HasValue &&
                                progressItem.count.Value > 0 && progressItem.total.Value > 0)
                            {
                                // Initialize tracking data if this is the first time
                                if (!progressItem.firstTrackedTime.HasValue)
                                {
                                    progressItem.firstTrackedTime = currentTime;
                                    progressItem.firstTrackedCount = progressItem.count.Value;
                                }
                                else if (queueItem.LastReport?.Progress?.ContainsKey(progressKey) == true)
                                {
                                    // Preserve tracking data from previous report
                                    var previousItem = queueItem.LastReport.Progress[progressKey];
                                    if (previousItem.firstTrackedTime.HasValue)
                                    {
                                        progressItem.firstTrackedTime = previousItem.firstTrackedTime;
                                        progressItem.firstTrackedCount = previousItem.firstTrackedCount;
                                    }
                                }

                                // Calculate speed and ETA if we have tracking data
                                if (progressItem.firstTrackedTime.HasValue && progressItem.firstTrackedCount.HasValue)
                                {
                                    var elapsedSeconds = (currentTime - progressItem.firstTrackedTime.Value).TotalSeconds;
                                    var itemsProcessed = progressItem.count.Value - progressItem.firstTrackedCount.Value;

                                    // Only calculate speed if at least 5 seconds have elapsed and items were processed
                                    // This provides more stable estimates for longer-running tasks
                                    if (elapsedSeconds >= 5 && itemsProcessed > 0)
                                    {
                                        // Calculate items per second
                                        progressItem.itemsPerSecond = itemsProcessed / elapsedSeconds;

                                        // Calculate estimated time remaining
                                        var itemsRemaining = progressItem.total.Value - progressItem.count.Value;
                                        if (itemsRemaining > 0 && progressItem.itemsPerSecond.Value > 0)
                                        {
                                            progressItem.estimatedSecondsRemaining = itemsRemaining / progressItem.itemsPerSecond.Value;
                                        }
                                        else
                                        {
                                            progressItem.estimatedSecondsRemaining = 0;
                                        }
                                    }
                                    else
                                    {
                                        // Not enough time elapsed yet - keep estimates null
                                        progressItem.itemsPerSecond = null;
                                        progressItem.estimatedSecondsRemaining = null;
                                    }
                                }
                            }
                        }
                    }

                    // update the last report
                    queueItem.LastReport = report;

                    return Ok();
                }
            }

            return Ok();
        }
    }
}