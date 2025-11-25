using hasheous_server.Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Tags Controller
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = false)]
    public class TagsController : ControllerBase
    {

        /// <summary>
        /// Retrieves the list of tags.
        /// </summary>
        /// <returns>A list of tags.</returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTags(DataObjectItemTags.TagType? type = null, string? search = null)
        {
            var dataObjects = new DataObjects();
            var tags = await dataObjects.GetTags();

            if (type.HasValue)
            {
                if (tags.ContainsKey(type.Value))
                {
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var filteredTags = tags[type.Value].Tags
                            .Where(t => t.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        return Ok(filteredTags);
                    }
                    else
                    {
                        return Ok(tags[type.Value].Tags);
                    }
                }
                else
                {
                    return Ok(new List<DataObjectItemTags.TagModel>());
                }
            }
            else
            {
                if (search != null)
                {
                    var filteredTags = new Dictionary<DataObjectItemTags.TagType, List<DataObjectItemTags.TagModel>>();
                    foreach (var tagGroup in tags)
                    {
                        var matchingTags = tagGroup.Value.Tags
                            .Where(t => t.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        if (matchingTags.Any())
                        {
                            filteredTags[tagGroup.Key] = matchingTags;
                        }
                    }
                    return Ok(filteredTags);
                }
                else
                {
                    return Ok(tags);
                }
            }
        }

        /// <summary>
        /// Retrieves the list of tags.
        /// </summary>
        /// <returns>A list of tags.</returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [Authorize]
        [Route("{TagType}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTagsByType(DataObjectItemTags.TagType TagType, string? search = null)
        {
            return await GetTags(TagType, search);
        }
    }
}