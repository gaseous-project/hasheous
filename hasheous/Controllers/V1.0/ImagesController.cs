using Classes;
using hasheous_server.Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ApiVersion("1.0")]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("{Id}")]
        [ResponseCache(CacheProfileName = "MaxDays")]
        [AllowAnonymous]
        public async Task<IActionResult> GetImage(string Id)
        {
            Images images = new Images();

            ImageItem image = images.GetImage(Id);

            if (image == null)
            {
                return NotFound();
            }
            else
            {
                return File(image.content, image.mimeType, image.Id + image.extension);
            }
        }

        [MapToApiVersion("1.0")]
        [MapToApiVersion("1.1")]
        [HttpPost]
        [ProducesResponseType(typeof(List<IFormFile>), StatusCodes.Status200OK)]
        [RequestSizeLimit(long.MaxValue)]
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit, RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
        [Authorize(Roles = "Admin, Moderator")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            Guid sessionid = Guid.NewGuid();

            string imageHash = "";

            if (file.Length > 0)
            {
                using (var ms = new MemoryStream())
                {
                    file.CopyTo(ms);
                    byte[] fileBytes = ms.ToArray();

                    Images images = new Images();
                    imageHash = images.AddImage(file.FileName, fileBytes);
                }
            }

            return Ok(imageHash);
        }
    }
}