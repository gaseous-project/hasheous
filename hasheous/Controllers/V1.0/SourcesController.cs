using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiVersion("1.0")]
    public class SourcesController : ControllerBase
    {
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSources(RomSignatureObject.Game.Rom.SignatureSourceType sourceType)
        {
            switch (sourceType)
            {
                case RomSignatureObject.Game.Rom.SignatureSourceType.None:
                    return Ok();
                case RomSignatureObject.Game.Rom.SignatureSourceType.TOSEC:
                case RomSignatureObject.Game.Rom.SignatureSourceType.MAMEArcade:
                case RomSignatureObject.Game.Rom.SignatureSourceType.MAMEMess:
                case RomSignatureObject.Game.Rom.SignatureSourceType.NoIntros:
                    Sources sources = new Sources();
                    return Ok(sources.GetSources(sourceType));

                default:
                    return Ok();
            }
        }
    }
}