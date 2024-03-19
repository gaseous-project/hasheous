using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Classes;
using Microsoft.AspNetCore.Authorization;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [Authorize(Roles = "Admin")]
    public class BackgroundTasksController : Controller
    {
        [HttpGet]
        [MapToApiVersion("1.0")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public List<ProcessQueue.QueueItem> GetQueue()
        {
            return ProcessQueue.QueueItems;
        }

        [HttpGet]
        [MapToApiVersion("1.0")]
        [Route("{TaskType}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<ProcessQueue.QueueItem> ForceRun(ProcessQueue.QueueItemType TaskType, Boolean ForceRun)
        {
            foreach (ProcessQueue.QueueItem qi in ProcessQueue.QueueItems)
            {
                if (qi.AllowManualStart == true)
                {
                    if (TaskType == qi.ItemType)
                    {
                        if (ForceRun == true)
                        {
                            qi.ForceExecute();
                        }
                        return qi;
                    }
                }
            }

            return NotFound();
        }
    }
}