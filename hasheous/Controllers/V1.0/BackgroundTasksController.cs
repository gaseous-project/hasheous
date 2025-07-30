using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public List<QueueProcessor.QueueItem> GetQueue()
        {
            return QueueProcessor.QueueItems;
        }

        [HttpGet]
        [MapToApiVersion("1.0")]
        [Route("{TaskType}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<QueueProcessor.QueueItem> ForceRun(QueueItemType TaskType, Boolean ForceRun)
        {
            foreach (QueueProcessor.QueueItem qi in QueueProcessor.QueueItems)
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