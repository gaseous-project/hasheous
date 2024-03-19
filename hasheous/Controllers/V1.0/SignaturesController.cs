using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [Authorize]
    public class SignaturesController : ControllerBase
    {
        [MapToApiVersion("1.0")]
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("Publishers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPublishers(string searchString)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM Signatures_Publishers WHERE Publisher LIKE @name";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "name", '%' + searchString + '%' }
            };

            List<Dictionary<string, object>> data = db.ExecuteCMDDict(sql, dbDict);

            return Ok(data);
        }
    }
}

