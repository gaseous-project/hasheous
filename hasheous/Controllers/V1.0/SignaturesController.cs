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
        [HttpPost]
        [AllowAnonymous]
        [Route("Search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSearch(SignatureSearchModel model)
        {
            SignatureManagement signature = new SignatureManagement();

            object[] objects = signature.SearchSignatures(model);

            return Ok(objects);
        }
    }
}

