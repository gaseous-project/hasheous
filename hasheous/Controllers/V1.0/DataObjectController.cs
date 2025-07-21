using System.Data;
using Authentication;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous.Classes;
using hasheous_server.Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize]
    public class DataObjectsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;

        public DataObjectsController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILoggerFactory loggerFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = loggerFactory.CreateLogger<AccountController>();
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(DataObjectDefinition), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [AllowAnonymous]
        [Route("{ObjectType}/Definition")]
        public async Task<IActionResult> DataObjectDefinition(Classes.DataObjects.DataObjectType ObjectType)
        {
            try
            {
                var DataObjectDefinition = hasheous_server.Classes.DataObjects.DataObjectDefinitions[ObjectType];

                return Ok(DataObjectDefinition);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [Route("{ObjectType}")]
        public async Task<IActionResult> DataObjectsList(Classes.DataObjects.DataObjectType ObjectType, string? search, int pageNumber = 0, int pageSize = 0, bool getchildrelations = false, AttributeItem.AttributeName? filterAttribute = null, string? filterValue = null, bool getMetadata = true, bool cacheAhead = true, bool cacheBehind = true)
        {
            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            hasheous_server.Models.DataObjectsList? objectsList;

            var user = await _userManager.GetUserAsync(User);

            // check the redis cache for the data
            // build a cache key from the url including the query parameters
            string unencodedCacheKey = Request.Path + "?";
            foreach (var query in Request.Query)
            {
                string? value = query.Value.ToString();
                if (query.Key == "pageNumber")
                {
                    value = pageNumber.ToString();
                }
                unencodedCacheKey += $"{query.Key}={value}&";
            }

            // remove the trailing '&' if it exists
            if (unencodedCacheKey.EndsWith("&"))
            {
                unencodedCacheKey = unencodedCacheKey.TrimEnd('&');
            }
            // generate a cache key for the data object
            string userId = "Anonymous";
            if (user != null)
            {
                userId = user.Id;
            }
            string cacheKey = RedisConnection.GenerateKey($"{userId}DataObjectList", ObjectType.ToString() + unencodedCacheKey);

            if (Config.RedisConfiguration.Enabled)
            {
                if (ObjectType != Classes.DataObjects.DataObjectType.App)
                {
                    string? cachedData = hasheous.Classes.RedisConnection.GetDatabase(0).StringGet(cacheKey);
                    if (cachedData != null)
                    {
                        // if cached data is found, deserialize it and return
                        DataObjectsList? dataObjectsList = Newtonsoft.Json.JsonConvert.DeserializeObject<DataObjectsList>(cachedData);

                        if (dataObjectsList != null)
                        {
                            return Ok(dataObjectsList);
                        }
                    }
                }
            }

            objectsList = await DataObjects.GetDataObjects(ObjectType, pageNumber, pageSize, search, getchildrelations, getMetadata, filterAttribute, filterValue, user);

            if (objectsList != null && Config.RedisConfiguration.Enabled)
            {
                if (ObjectType != Classes.DataObjects.DataObjectType.App)
                {
                    // store the data in the cache
                    hasheous.Classes.RedisConnection.GetDatabase(0).StringSet(cacheKey, Newtonsoft.Json.JsonConvert.SerializeObject(objectsList), TimeSpan.FromMinutes(5));

                    if (cacheBehind)
                    {
                        // cache the previous page if it exists
                        if (pageNumber > 1)
                        {
                            // call the same method with the previous page number
                            _ = DataObjectsList(ObjectType, search, pageNumber - 1, pageSize, getchildrelations, filterAttribute, filterValue, getMetadata, false, false);
                        }
                    }

                    if (cacheAhead)
                    {
                        // cache the next page if it exists
                        if (pageNumber < objectsList.TotalPages)
                        {
                            // call the same method with the next page number
                            _ = DataObjectsList(ObjectType, search, pageNumber + 1, pageSize, getchildrelations, filterAttribute, filterValue, getMetadata, false, false);
                        }
                    }
                }
            }

            return Ok(objectsList);
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [AllowAnonymous]
        [Route("{ObjectType}/{Id}")]
        public async Task<IActionResult> GetDataObject(Classes.DataObjects.DataObjectType ObjectType, long Id)
        {
            // Validate ObjectType
            if (!Enum.IsDefined(typeof(Classes.DataObjects.DataObjectType), ObjectType))
            {
                return BadRequest("Invalid ObjectType provided.");
            }

            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

            if (DataObject == null || DataObject.ObjectType != ObjectType)
            {
                return NotFound();
            }
            else
            {
                if (ObjectType == Classes.DataObjects.DataObjectType.App)
                {
                    var user = await _userManager.GetUserAsync(User);

                    if (user == null)
                    {
                        // no logged in user, check that the app is public
                        DataObject.Permissions = new List<DataObjectPermission.PermissionType>
                        {
                            DataObjectPermission.PermissionType.Read
                        };

                        if (DataObject.Attributes.Any(a => a.attributeName == AttributeItem.AttributeName.Public && a.Value.ToString() == "1"))
                        {
                            return Ok(DataObject);
                        }
                        else
                        {
                            return Unauthorized();
                        }
                    }

                    DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);
                    DataObject.Permissions = dataObjectPermission.GetObjectPermission(user, ObjectType, DataObject.Id);
                    if (DataObject.Permissions.Contains(DataObjectPermission.PermissionType.Update))
                    {
                        DataObject.UserPermissions = dataObjectPermission.GetObjectPermissionList(DataObject.Id);
                    }
                }

                return Ok(DataObject);
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}")]
        public async Task<IActionResult> NewDataObject(Classes.DataObjects.DataObjectType ObjectType, Models.DataObjectItemModel model)
        {
            // check permission
            var user = await _userManager.GetUserAsync(User);
            DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

            if (dataObjectPermission.CheckAsync(user, ObjectType, DataObjectPermission.PermissionType.Create).Result)
            {
                hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

                Models.DataObjectItem? DataObject = await DataObjects.NewDataObject(ObjectType, model, user);

                if (DataObject == null)
                {
                    return NotFound();
                }
                else
                {
                    return Ok(DataObject);
                }
            }
            else
            {
                return Unauthorized();
            }
        }

        [MapToApiVersion("1.0")]
        [HttpDelete]
        [Authorize(Roles = "Admin,Moderator")]
        [Route("{ObjectType}/{Id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDataObject(Classes.DataObjects.DataObjectType ObjectType, long Id)
        {
            // check permission
            var user = await _userManager.GetUserAsync(User);
            DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

            if (dataObjectPermission.CheckAsync(user, ObjectType, DataObjectPermission.PermissionType.Delete, Id).Result)
            {
                hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

                Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

                if (DataObject == null)
                {
                    return NotFound();
                }
                else
                {
                    DataObjects.DeleteDataObject(ObjectType, Id);
                    return Ok();
                }
            }
            else
            {
                return Unauthorized();
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPut]
        [Authorize(Roles = "Admin,Moderator")]
        [Route("{ObjectType}/{Id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EditDataObject(Classes.DataObjects.DataObjectType ObjectType, long Id, Models.DataObjectItemModel model)
        {
            // check permission
            var user = await _userManager.GetUserAsync(User);
            DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

            if (dataObjectPermission.CheckAsync(user, ObjectType, DataObjectPermission.PermissionType.Update, Id).Result)
            {
                hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

                Models.DataObjectItem? DataObject = await DataObjects.EditDataObject(ObjectType, Id, model);

                if (DataObject == null)
                {
                    return NotFound();
                }
                else
                {
                    return Ok(DataObject);
                }
            }
            else
            {
                return Unauthorized();
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPut]
        // [Authorize(Roles = "Admin,Moderator,Member")]
        [Authorize]
        [Route("{ObjectType}/{Id}/FullObject")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EditDataObjectFull(Classes.DataObjects.DataObjectType ObjectType, long Id, Models.DataObjectItem model)
        {
            if (ModelState.IsValid)
            {
                // check permission
                var user = await _userManager.GetUserAsync(User);
                DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

                if (dataObjectPermission.CheckAsync(user, ObjectType, DataObjectPermission.PermissionType.Update, Id).Result)
                {
                    hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

                    Models.DataObjectItem? DataObject = await DataObjects.EditDataObject(ObjectType, Id, model);

                    if (DataObject == null)
                    {
                        return NotFound();
                    }
                    else
                    {
                        return Ok(DataObject);
                    }
                }
                else
                {
                    return Unauthorized();
                }
            }
            else
            {
                return BadRequest(ModelState);
            }
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}/{Id}/Attributes")]
        public async Task<IActionResult> GetDataObjectAttributes(Classes.DataObjects.DataObjectType ObjectType, long Id)
        {
            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(DataObjects.GetAttributes(Id, true));
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}/{Id}/Attributes")]
        public async Task<IActionResult> NewDataObjectAttribute(Classes.DataObjects.DataObjectType ObjectType, long Id, AttributeItem model)
        {
            // check permission
            var user = await _userManager.GetUserAsync(User);
            DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

            if (dataObjectPermission.CheckAsync(user, ObjectType, DataObjectPermission.PermissionType.Update, Id).Result)
            {
                hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

                Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

                if (DataObject == null)
                {
                    return NotFound();
                }
                else
                {
                    AttributeItem attributeItem = await DataObjects.AddAttribute(Id, model);

                    return Ok(attributeItem);
                }
            }
            else
            {
                return Unauthorized();
            }
        }

        [MapToApiVersion("1.0")]
        [HttpDelete]
        [Authorize(Roles = "Admin,Moderator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}/{Id}/Attributes/{AttributeId}")]
        public async Task<IActionResult> DeleteDataObjectAttribute(Classes.DataObjects.DataObjectType ObjectType, long Id, long AttributeId)
        {
            // check permission
            var user = await _userManager.GetUserAsync(User);
            DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

            if (dataObjectPermission.CheckAsync(user, ObjectType, DataObjectPermission.PermissionType.Update, Id).Result)
            {
                hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

                Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

                if (DataObject == null)
                {
                    return NotFound();
                }
                else
                {
                    DataObjects.DeleteAttribute(Id, AttributeId);

                    return Ok();
                }
            }
            else
            {
                return Unauthorized();
            }
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}/{Id}/SignatureMap")]
        public async Task<IActionResult> GetDataObjectSignatureMap(Classes.DataObjects.DataObjectType ObjectType, long Id)
        {
            // signatures aren't valid for apps
            if (ObjectType != Classes.DataObjects.DataObjectType.App)
            {
                return NotFound();
            }

            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(DataObjects.GetSignatures(ObjectType, Id));
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}/{Id}/SignatureMap")]
        public async Task<IActionResult> NewDataObjectSignatureMap(Classes.DataObjects.DataObjectType ObjectType, long Id, long SignatureId)
        {
            // signatures aren't valid for apps
            if (ObjectType != Classes.DataObjects.DataObjectType.App)
            {
                return NotFound();
            }

            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                DataObjects.AddSignature(Id, ObjectType, SignatureId);

                return Ok(SignatureId);
            }
        }

        [MapToApiVersion("1.0")]
        [HttpDelete]
        [Authorize(Roles = "Admin,Moderator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}/{Id}/SignatureMap/{SignatureMapId}")]
        public async Task<IActionResult> DeleteDataObjectSignatureMap(Classes.DataObjects.DataObjectType ObjectType, long Id, long SignatureId)
        {
            // signatures aren't valid for apps
            if (ObjectType != Classes.DataObjects.DataObjectType.App)
            {
                return NotFound();
            }

            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                DataObjects.DeleteSignature(Id, ObjectType, SignatureId);

                return Ok();
            }
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}/{Id}/MetadataMap")]
        public async Task<IActionResult> GetDataObjectMetadataMap(Classes.DataObjects.DataObjectType ObjectType, long Id, bool forceScan = false)
        {
            // metadata mappings aren't valid for apps
            if (ObjectType == Classes.DataObjects.DataObjectType.App)
            {
                return NotFound();
            }

            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                // only users who hold the admin or moderator role should be able to force a scan
                // check if the logged in user has the required role
                if (!User.IsInRole("Admin") && !User.IsInRole("Moderator"))
                {
                    forceScan = false;
                }

                if (forceScan)
                {
                    DataObjects.DataObjectMetadataSearch(ObjectType, Id, true);
                }

                return Ok(DataObjects.GetMetadataMap(ObjectType, Id));
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}/{Id}/SignatureSearch/")]
        public async Task<IActionResult> GetSignatureSearch(Classes.DataObjects.DataObjectType ObjectType, long Id, string SearchString)
        {
            // signatures aren't valid for apps
            if (ObjectType != Classes.DataObjects.DataObjectType.App)
            {
                return NotFound();
            }

            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(DataObjects.SignatureSearch(Id, ObjectType, SearchString));
            }
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [Authorize(Roles = "Admin,Moderator")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("{ObjectType}/{Id}/MergeObject/")]
        public async Task<IActionResult> MergeObjects(Classes.DataObjects.DataObjectType ObjectType, long Id, long TargetId, bool Commit = false)
        {
            // merging is only valid for games, platforms, and companies
            Classes.DataObjects.DataObjectType[] validTypes = [
                Classes.DataObjects.DataObjectType.Game,
                Classes.DataObjects.DataObjectType.Platform,
                Classes.DataObjects.DataObjectType.Company
            ];
            if (!validTypes.Contains(ObjectType))
            {
                return NotFound();
            }

            // check permission
            // admins can merge any objects
            // moderators can only merge game and company objects
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            // get user roles
            var roles = await _userManager.GetRolesAsync(user);
            string[] validRoles = ["Admin", "Moderator"];
            // check if the user has the required role
            // admins can merge any objects, moderators can only merge game and company objects
            if (!roles.Any(role => validRoles.Contains(role)))
            {
                return Unauthorized();
            }

            Classes.DataObjects.DataObjectType[] moderatorValidTypes = [
                Classes.DataObjects.DataObjectType.Game,
                Classes.DataObjects.DataObjectType.Company
            ];
            if (!roles.Contains("Admin") && roles.Contains("Moderator") && !moderatorValidTypes.Contains(ObjectType))
            {
                return Unauthorized();
            }

            // check if the target object is of the same type
            hasheous_server.Classes.DataObjects DataObjects = new Classes.DataObjects();

            Models.DataObjectItem? DataObject = await DataObjects.GetDataObject(ObjectType, Id);

            if (DataObject == null)
            {
                return NotFound();
            }
            else
            {
                Models.DataObjectItem? TargetDataObject = await DataObjects.GetDataObject(ObjectType, TargetId);

                if (TargetDataObject == null)
                {
                    return NotFound();
                }
                else
                {
                    // check if the target object is of the same type
                    if (DataObject.ObjectType != TargetDataObject.ObjectType)
                    {
                        return BadRequest("The target object must be of the same type as the source object.");
                    }

                    var result = DataObjects.MergeObjects(DataObject, TargetDataObject, Commit);
                    return Ok(result);
                }
            }
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(List<ClientApiKeyItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("app/{Id}/ClientApiKeys")]
        public async Task<IActionResult> GetClientApiKeys(long Id)
        {
            var user = await _userManager.GetUserAsync(User);

            DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

            if (dataObjectPermission.CheckAsync(user, DataObjects.DataObjectType.App, DataObjectPermission.PermissionType.Update, Id).Result)
            {
                Authentication.ClientApiKey clientApiKey = new Authentication.ClientApiKey();

                return Ok(clientApiKey.GetApiKeys(Id));
            }
            else
            {
                return NotFound();
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("app/{Id}/ClientApiKeys")]
        public async Task<IActionResult> NewClientApiKey(long Id, string Name, DateTime? Expires)
        {
            var user = await _userManager.GetUserAsync(User);

            DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

            if (dataObjectPermission.CheckAsync(user, DataObjects.DataObjectType.App, DataObjectPermission.PermissionType.Update, Id).Result)
            {
                Authentication.ClientApiKey clientApiKey = new Authentication.ClientApiKey();

                return Ok(clientApiKey.CreateApiKey(Id, Name, Expires));
            }
            else
            {
                return NotFound();
            }
        }

        [MapToApiVersion("1.0")]
        [HttpDelete]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("app/{Id}/ClientApiKeys/{ClientId}")]
        public async Task<IActionResult> DeleteClientApiKey(long Id, long ClientId)
        {
            var user = await _userManager.GetUserAsync(User);

            DataObjectPermission dataObjectPermission = new DataObjectPermission(_userManager);

            if (dataObjectPermission.CheckAsync(user, DataObjects.DataObjectType.App, DataObjectPermission.PermissionType.Update, Id).Result)
            {
                Authentication.ClientApiKey clientApiKey = new Authentication.ClientApiKey();

                clientApiKey.RevokeApiKey(Id, ClientId);

                return Ok();
            }
            else
            {
                return NotFound();
            }
        }
    }
}