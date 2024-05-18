using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Classes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Handles log queries
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ApiVersion("1.0")]
    [Authorize(Roles = "Admin")]
    public class LogsController : Controller
    {
        /// <summary>
        /// Query the log for events
        /// </summary>
        /// <param name="model">Object used to query the logs</param>
        /// <returns>An array of log events</returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public List<Logging.LogItem> Logs(Logging.LogsViewModel model)
        {
            return Logging.GetLogs(model);
        }
    }
}