using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [Authorize]
    public class DataObjectsController : ControllerBase
    {
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [Route("{ObjectType}")]
        public async Task<IActionResult> DataObjectsList(Classes.DataObjects.DataObjectType ObjectType)
        {
            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            return Ok(DataObjects.GetDataObjects(ObjectType));
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [AllowAnonymous]
        [Route("{ObjectType}/{Id}")]
        public async Task<IActionResult> GetDataObject(Classes.DataObjects.DataObjectType ObjectType, long Id)
        {
            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = DataObjects.GetDataObject(ObjectType, Id);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(DataObject);
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}")]
        public async Task<IActionResult> NewDataObject(Classes.DataObjects.DataObjectType ObjectType, Models.DataObjectItemModel model)
        {
            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = DataObjects.NewDataObject(ObjectType, model);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(DataObject);
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPut]
        [Authorize(Roles = "Admin")]
        [Route("{ObjectType}/{Id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EditDataObject(Classes.DataObjects.DataObjectType ObjectType, long Id, Models.DataObjectItemModel model)
        {
            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = DataObjects.EditDataObject(ObjectType, Id, model);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(DataObject);
            }
        }
    }
}