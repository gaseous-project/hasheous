using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Authentication;
using Classes;
using Classes.Insights;
using hasheous_server.Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("localisation/")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ApiVersion("1.0")]
    [AllowAnonymous]
    public class LocalisationController : ControllerBase
    {
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Route("languages.json")]
        public async Task<IActionResult> GetLanguages()
        {
            List<LocalisationItem> languages = await Localisation.GetLanguages();
            return Ok(languages);
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("request/{locale}.json")]
        public async Task<IActionResult> RequestLocalisation(string locale)
        {
            // check that the requested locale is valid and supported
            if (string.IsNullOrEmpty(locale))
            {
                return NotFound();
            }
            if (!System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
                .Any(c => c.Name == locale))
            {
                return NotFound();
            }

            // generate a task to generate the requested language
            string baseLanguage = locale.Split('-')[0];
            string? variantLanguage = locale.Contains('-') ? locale.Split('-')[1] : null;

            var baseLanguageCulture = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
                .FirstOrDefault(c => c.Name == baseLanguage);
            if (baseLanguageCulture == null)
            {
                return NotFound();
            }

            var variantLanguageCulture = variantLanguage != null ? System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
                .FirstOrDefault(c => c.Name == locale) : null;
            if (variantLanguage != null && variantLanguageCulture == null)
            {
                return NotFound();
            }

            var dataObjects = new DataObjects();

            // check for a dataobject related to the base language
            var baseLanguageDataObject = await dataObjects.SearchDataObject(DataObjects.DataObjectType.Language, baseLanguage, null);

            hasheous_server.Models.DataObjectItem baseLanguaguageDataObjectItem;
            if (baseLanguageDataObject == null)
            {
                // object does not exist, generate a new language data object
                baseLanguaguageDataObjectItem = await dataObjects.NewDataObject(DataObjects.DataObjectType.Language, new DataObjectItemModel
                {
                    IsBlockedFromMatching = true,
                    Name = baseLanguage
                });
                baseLanguaguageDataObjectItem.Attributes = new List<AttributeItem>
                {
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.EnglishName,
                        attributeType = AttributeItem.AttributeType.ShortString,
                        attributeRelationType = DataObjects.DataObjectType.None,
                        Value = baseLanguageCulture.EnglishName
                    },
                    new AttributeItem{
                        attributeName = AttributeItem.AttributeName.NativeName,
                        attributeType = AttributeItem.AttributeType.ShortString,
                        attributeRelationType = DataObjects.DataObjectType.None,
                        Value = baseLanguageCulture.NativeName
                    }
                };
                await dataObjects.EditDataObject(DataObjects.DataObjectType.Language, baseLanguaguageDataObjectItem.Id, baseLanguaguageDataObjectItem);
            }
            else
            {
                baseLanguaguageDataObjectItem = baseLanguageDataObject;
            }

            // check if the data object has a localisation task already queued - queue if not, otherwise do nothing
            var baseLanguaguageTasks = hasheous_server.Classes.Tasks.Clients.TaskManagement.GetAllTasks(baseLanguaguageDataObjectItem.Id);
            Models.Tasks.QueueItemModel? baseLanguageTask = baseLanguaguageTasks.Find(t => t.TaskName == Models.Tasks.TaskType.AILocalisationGeneration);
            if (baseLanguageTask == null)
            {
                // queue a task to generate the localisation for this new data object
                baseLanguageTask = hasheous_server.Classes.Tasks.Clients.TaskManagement.EnqueueTask(baseLanguaguageDataObjectItem.Id, Models.Tasks.TaskType.AILocalisationGeneration, new List<Models.Tasks.Capabilities> { Models.Tasks.Capabilities.Internet, Models.Tasks.Capabilities.AI, Models.Tasks.Capabilities.DiskSpace }, new Dictionary<string, string> { { "language", baseLanguage } });
            }

            // generate a task to generate the variant language if it exists
            if (!String.IsNullOrEmpty(variantLanguage))
            {
                var variantLanguageDataObject = locale != null ? await dataObjects.SearchDataObject(DataObjects.DataObjectType.Language, locale, null) : null;
                if (variantLanguageDataObject == null)
                {
                    // object does not exist, generate a new language data object for the variant
                    variantLanguageDataObject = await dataObjects.NewDataObject(DataObjects.DataObjectType.Language, new DataObjectItemModel
                    {
                        IsBlockedFromMatching = true,
                        Name = locale
                    });
                    variantLanguageDataObject.Attributes = new List<AttributeItem>
                    {
                        new AttributeItem{
                            attributeName = AttributeItem.AttributeName.EnglishName,
                            attributeType = AttributeItem.AttributeType.ShortString,
                            attributeRelationType = DataObjects.DataObjectType.None,
                            Value = variantLanguageCulture.EnglishName
                        },
                        new AttributeItem{
                            attributeName = AttributeItem.AttributeName.NativeName,
                            attributeType = AttributeItem.AttributeType.ShortString,
                            attributeRelationType = DataObjects.DataObjectType.None,
                            Value = variantLanguageCulture.NativeName
                        },
                        new AttributeItem
                        {
                            attributeName = AttributeItem.AttributeName.ParentLanguage,
                            attributeType = AttributeItem.AttributeType.ObjectRelationship,
                            attributeRelationType = DataObjects.DataObjectType.Language,
                            Value = baseLanguaguageDataObjectItem.Id
                        }
                    };
                    await dataObjects.EditDataObject(DataObjects.DataObjectType.Language, variantLanguageDataObject.Id, variantLanguageDataObject);
                }

                // check if the data object has a localisation task already queued - queue if not, otherwise do nothing
                var variantLanguaguageTasks = hasheous_server.Classes.Tasks.Clients.TaskManagement.GetAllTasks(variantLanguageDataObject.Id);
                if (variantLanguaguageTasks.Find(t => t.TaskName == Models.Tasks.TaskType.AILocalisationGenerationRegional) == null)
                {
                    // queue a task to generate the localisation for this new data object
                    hasheous_server.Classes.Tasks.Clients.TaskManagement.EnqueueTask(variantLanguageDataObject.Id, Models.Tasks.TaskType.AILocalisationGenerationRegional, new List<Models.Tasks.Capabilities> { Models.Tasks.Capabilities.Internet, Models.Tasks.Capabilities.AI, Models.Tasks.Capabilities.DiskSpace }, new Dictionary<string, string> { { "language", locale } }, baseLanguageTask.Id);
                }
            }

            return Ok();
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("{locale}.json")]
        public async Task<IActionResult> GetLocalisation(string locale)
        {
            // ensure locale is valid since we'll likely be hitting the disk
            string localePattern = @"^[a-z]{2}(?:-[A-Z]{2}|_[A-Z]{2})?$";
            if (!Regex.IsMatch(locale, localePattern))
            {
                return NotFound();
            }

            // try to load the requested localisation item
            try
            {
                LocalisationItem? item = await Localisation.GetLocalisationItem(locale);
                if (item != null)
                {
                    // merge missing translations from the English localisation item
                    if (locale == "en")
                    {
                        return Ok(item);
                    }
                    else
                    {
                        return Ok(item);
                    }
                }
                return NotFound();
            }
            catch (Exception ex)
            {
                // Log the exception if necessary
                return NotFound(ex.Message);
            }
        }
    }
}