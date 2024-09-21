using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Sources Controller
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [Authorize]
    public class SourcesController : ControllerBase
    {
        /// <summary>
        /// Get Source List
        /// </summary>
        /// <returns>
        /// The list of signature sources
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [Route("")]
        public async Task<IActionResult> SourceList()
        {
            return Ok(Enum.GetValues(typeof(RomSignatureObject.Game.Rom.SignatureSourceType)).Cast<RomSignatureObject.Game.Rom.SignatureSourceType>().ToList());
        }

        /// <summary>
        /// Get Source Statistics
        /// </summary>
        /// <returns>
        /// The list of signature sources
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [Route("Statistics")]
        public async Task<IActionResult> GetSourceStatistics()
        {
            hasheous_server.Classes.Sources sources = new hasheous_server.Classes.Sources();
            return Ok(sources.GetSourceStatistics());
        }

        /// <summary>
        /// Get Source Details
        /// </summary>
        /// <param name="sourceType">
        /// The source type to get details for
        /// </param>
        /// <returns>
        /// The list of signature sources
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [Route("{sourceType}/Details")]
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
                    hasheous_server.Classes.Sources sources = new hasheous_server.Classes.Sources();
                    return Ok(sources.GetSources(sourceType));

                default:
                    return Ok();
            }
        }
    }
}