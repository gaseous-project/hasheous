using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Signatures Controller
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class SignaturesController : ControllerBase
    {
        /// <summary>
        /// Get Signatures
        /// </summary>
        /// <param name="model">
        /// The model to search for
        /// </param>
        /// <returns>
        /// The list of signatures
        /// </returns>
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

        /// <summary>
        /// Get Rom Item By Hash
        /// </summary>
        /// <param name="model">
        /// The model to search for
        /// </param>
        /// <returns>
        /// The list of signatures
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpPost]
        [AllowAnonymous]
        [Route("Rom/ByHash")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRomItemByHash(HashLookupModel model)
        {
            SignatureManagement signature = new SignatureManagement();

            object objects = signature.GetRomItemByHash(model);

            return Ok(objects);
        }

        /// <summary>
        /// Get Rom Item By Id
        /// </summary>
        /// <param name="id">
        /// The id to search for
        /// </param>
        /// <returns>
        /// The list of signatures
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [AllowAnonymous]
        [Route("Rom/ById/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetRomItemById(long id)
        {
            SignatureManagement signature = new SignatureManagement();

            object objects = signature.GetRomItemById(id);

            return Ok(objects);
        }
    }
}

