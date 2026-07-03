using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Net;
using System.Web;
using Classes;
using Classes.Insights;
using Classes.Metadata;
using hasheous.Classes;
using hasheous_server.Classes.Metadata;
using hasheous_server.Classes.Metadata.IGDB;
using HasheousClient;
using IGDB;
using IGDB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheGamesDB.SQL;
using static Authentication.ClientApiKey;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Endpoints used for proxying metadata from metadata sources like IGDB and TheGamesDB.
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [ClientApiKey()]
    [IgnoreAntiforgeryToken]
    [Insight(Insights.InsightSourceType.MetadataProxy)]
    public class MetadataProxyController : ControllerBase
    {
        private FileStreamResult FileWithManagedStream(ResolvedContentStream resolvedStream, string contentType, string? fileDownloadName = null)
        {
            HttpContext.Response.RegisterForDispose(resolvedStream);

            if (string.IsNullOrWhiteSpace(fileDownloadName))
            {
                return File(resolvedStream.Stream, contentType);
            }

            return File(resolvedStream.Stream, contentType, fileDownloadName);
        }

        #region IGDB
        /// <summary>
        /// Get metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the metadata object to fetch
        /// </param>
        /// <param name="slug">
        /// The slug of the metadata object to fetch - note that this is optional and not all metadata types have slugs.
        /// </param>
        /// <param name="expandColumns">
        /// A comma-separated list of columns to expand in the metadata object.
        /// This is optional and can be used to fetch additional data related to the metadata object.
        /// </param>
        /// <param name="MetadataType">
        /// The type of metadata to fetch, e.g. "Game", "Artwork", etc.
        /// This should match the class name in IGDB.Models namespace.
        /// </param>
        /// <returns>
        /// The metadata object from IGDB
        /// </returns>
        /// <remarks>
        /// 
        /// </remarks>
        ///    
        /// <response code="200">Returns the metadata object from IGDB</response>
        /// <response code="404">If the metadata object is not found</response>
        /// <response code="400">If the Id or slug is invalid</response>
        /// <response code="500">If an error occurs</response>
        ///
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/{MetadataType}")]
        [ResponseCache(CacheProfileName = "7Days")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> GetMetadata(string MetadataType, long Id, string slug = "", string expandColumns = "")
        {
            // check that MetadataType is a valid class in IGDB.Models
            var igdbAssembly = typeof(IGDB.Models.Game).Assembly;
            Type? metadataType = igdbAssembly.GetType($"IGDB.Models.{MetadataType}", false, true);
            if (metadataType == null)
            {
                return BadRequest(new Dictionary<string, string> { { "Error", $"Metadata type '{MetadataType}' not found in IGDB.Models namespace." } });
            }

            // If valid, continue with your logic (e.g., call _GetMetadata)
            return await _GetMetadata(MetadataType, Id, slug, expandColumns);
        }

        private async Task<IActionResult> _GetMetadata(string routeName, long Id, string slug = "", string expandColumns = "")
        {
            // reject invalid id or slug
            if (Id == 0 && slug == "")
            {
                return BadRequest();
            }

            // check cache first
            string cacheKey = RedisConnection.GenerateKey("MetadataProxy-IGDB", routeName + Id.ToString() + slug + expandColumns);

            if (Config.RedisConfiguration.Enabled)
            {
                if (await RedisConnection.CacheItemExists(cacheKey))
                {
                    return Ok(await RedisConnection.GetCacheItem<Dictionary<string, object>>(cacheKey));
                }
            }

            // define variable for slug response
            bool isSlugSearch = false;
            if (Id == 0 && slug != "")
            {
                isSlugSearch = true;
            }
            bool supportsSlugSearch = false;

            // define return value
            object? returnValue = null;

            // look for a type named "IGDB.Models.{routeName}"
            var igdbAssembly = typeof(IGDB.Models.Game).Assembly;
            Type? metadataType = igdbAssembly.GetType($"IGDB.Models.{routeName}", false, true);
            if (metadataType == null)
            {
                return BadRequest(new Dictionary<string, string> { { "Error", $"Metadata type '{routeName}' not found." } });
            }

            // get the endpoint data for this type
            var getEndpointDataMethod = typeof(Classes.Metadata.IGDB.Metadata)
                .GetMethod("GetEndpointData", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var genericGetEndpointDataMethod = getEndpointDataMethod?.MakeGenericMethod(metadataType);
            var endpointData = genericGetEndpointDataMethod?.Invoke(null, null);

            // cast endpointData to EndpointDataItem
            if (endpointData == null || !(endpointData is Classes.Metadata.IGDB.Metadata.EndpointDataItem))
            {
                return BadRequest(new Dictionary<string, string> { { "Error", $"Metadata type '{routeName}' does not have endpoint data." } });
            }
            var endpointDataItem = (Classes.Metadata.IGDB.Metadata.EndpointDataItem)endpointData;

            // check if the type supports slug search
            if (endpointDataItem.SupportsSlugSearch == false && isSlugSearch)
            {
                return BadRequest(new Dictionary<string, string> { { "Error", $"Metadata type '{routeName}' does not support slug search." } });
            }

            // create a new instance of the metadata type
            object metadataInstance = Activator.CreateInstance(metadataType) ?? throw new InvalidOperationException($"Could not create instance of metadata type '{routeName}'.");

            // get the metadata and convert it to the correct type
            if (isSlugSearch)
            {
                var method = typeof(hasheous_server.Classes.Metadata.IGDB.Metadata)
                    .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .FirstOrDefault(m =>
                        m.Name == "GetMetadata" &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].Name == "Slug"
                    );
                var genericMethod = method?.MakeGenericMethod(metadataType);
                metadataInstance = await (dynamic)genericMethod?.Invoke(null, new object[] { slug });
            }
            else
            {
                var method = typeof(hasheous_server.Classes.Metadata.IGDB.Metadata)
                    .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .FirstOrDefault(m =>
                        m.Name == "GetMetadata" &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].Name == "Id"
                    );
                var genericMethod = method?.MakeGenericMethod(metadataType);
                metadataInstance = await (dynamic)genericMethod?.Invoke(null, new object[] { Id });
            }

            if (metadataInstance == null)
            {
                return NotFound(new Dictionary<string, string> { { "Error", $"Metadata object '{routeName}' with Id '{Id}' or slug '{slug}' not found." } });
            }

            // convert the metadata instance to the correct type
            var hasheousAssembly = typeof(HasheousClient.Models.Metadata.IGDB.Game).Assembly;
            Type? targetMetadataType = hasheousAssembly.GetType($"HasheousClient.Models.Metadata.IGDB.{routeName}", false, true);
            var convertMethod = typeof(HasheousClient.Models.Metadata.IGDB.ITools)
                .GetMethod("ConvertFromIGDB", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var genericConvertMethod = convertMethod?.MakeGenericMethod(targetMetadataType);
            returnValue = genericConvertMethod?.Invoke(null, new object[] { metadataInstance, Activator.CreateInstance(targetMetadataType) });

            if (returnValue == null)
            {
                return NotFound(new Dictionary<string, string> { { "Error", $"Metadata object '{routeName}' with Id '{Id}' or slug '{slug}' not found." } });
            }

            // convert returnValue to a Dictionary<string, object>
            Dictionary<string, object> returnValueDict = new Dictionary<string, object>();
            string[] expandColumnsArray = expandColumns
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();
            foreach (var property in returnValue.GetType().GetProperties())
            {
                if (property.CanRead)
                {
                    var value = property.GetValue(returnValue);
                    if (value != null)
                    {
                        // get the JsonProperty attribute if it exists
                        var jsonPropertyAttribute = property.GetCustomAttributes(typeof(Newtonsoft.Json.JsonPropertyAttribute), false)
                            .Cast<Newtonsoft.Json.JsonPropertyAttribute>()
                            .FirstOrDefault();

                        if (jsonPropertyAttribute != null)
                        {
                            // use the json property name as the key
                            string propertyName = jsonPropertyAttribute.PropertyName;

                            // process expand columns
                            if (expandColumnsArray.Length > 0 && (expandColumnsArray.Contains(propertyName, StringComparer.OrdinalIgnoreCase) || expandColumnsArray.Contains("*") || expandColumnsArray.Contains("all", StringComparer.OrdinalIgnoreCase)))
                            {
                                // check if the value is an array or a list
                                if (value is Array || value is System.Collections.IList)
                                {
                                    Dictionary<string, object> expandedValues = new Dictionary<string, object>();
                                    string expandedEndpoint = Metadata.GetEndpointFromSourceTypeAndFieldName(routeName, property.Name);
                                    if (string.IsNullOrEmpty(expandedEndpoint))
                                    {
                                        // if no endpoint is found, just add the value as is
                                        returnValueDict.Add(propertyName, value);
                                    }
                                    else
                                    {
                                        foreach (var item in (System.Collections.IEnumerable)value)
                                        {
                                            // recursively call _GetMetadata for each item, using the propertyName as the routeName, and the item as the Id
                                            long itemId = 0;
                                            if (item is long l)
                                            {
                                                itemId = l;
                                            }
                                            else if (item is int i)
                                            {
                                                itemId = i;
                                            }
                                            else
                                            {
                                                continue;
                                            }

                                            var itemMetadata = await _GetMetadata(expandedEndpoint, itemId);
                                            if (itemMetadata is OkObjectResult okResult && okResult.Value is Dictionary<string, object> itemDict)
                                            {
                                                expandedValues.Add(itemId.ToString(), itemDict);
                                            }
                                        }
                                        returnValueDict.Add(propertyName, expandedValues);
                                    }
                                }
                                else
                                {
                                    // recursively call _GetMetadata for the item, using the propertyName as the routeName, and the value as the Id
                                    if (value is long itemId)
                                    {
                                        string expandedEndpoint = Metadata.GetEndpointFromSourceTypeAndFieldName(routeName, property.Name);
                                        if (!string.IsNullOrEmpty(expandedEndpoint))
                                        {
                                            var itemMetadata = await _GetMetadata(expandedEndpoint, itemId);
                                            if (itemMetadata is OkObjectResult okResult && okResult.Value is Dictionary<string, object> itemDict)
                                            {
                                                returnValueDict.Add(propertyName, itemDict);
                                            }
                                        }
                                        else
                                        {
                                            returnValueDict.Add(propertyName, value);
                                        }
                                    }
                                    else
                                    {
                                        returnValueDict.Add(propertyName, value);
                                    }
                                }
                            }
                            else
                            {
                                returnValueDict.Add(propertyName, value);
                            }
                        }
                        else
                        {
                            returnValueDict.Add(property.Name, value);
                        }
                    }
                }
            }


            if (returnValueDict != null)
            {
                // store in cache
                if (Config.RedisConfiguration.Enabled)
                {
                    await RedisConnection.SetCacheItem<Dictionary<string, object>>(cacheKey, returnValueDict, TimeSpan.FromDays(1));
                }

                return Ok(returnValueDict);
            }
            else
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Search for metadata from IGDB
        /// </summary>
        /// <param name="SearchString">
        /// The string to search for
        /// </param>
        /// <returns>
        /// The platform metadata object from IGDB
        /// </returns>
        /// <remarks>
        /// 
        /// </remarks>
        /// 
        /// <response code="200">Returns the platform metadata object from IGDB</response>
        /// <response code="404">If the platform metadata object is not found</response>
        /// <response code="500">If an error occurs</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("IGDB/Search/Platform")]
        [ResponseCache(CacheProfileName = "None")]
        public async Task<IActionResult> SearchMetadata_Platform(string SearchString)
        {
            string searchBody = "";
            string searchFields = "fields abbreviation,alternative_name,category,checksum,created_at,generation,name,platform_family,platform_logo,slug,summary,updated_at,url,versions,websites; ";
            searchBody += "where name ~ *\"" + SearchString + "\"*;";

            // check cache first
            string cacheKey = RedisConnection.GenerateKey("MetadataProxy-IGDB", "Search-Platform" + SearchString);
            if (Config.RedisConfiguration.Enabled)
            {
                if (await RedisConnection.CacheItemExists(cacheKey))
                {
                    return Ok(await RedisConnection.GetCacheItem<List<HasheousClient.Models.Metadata.IGDB.Platform>>(cacheKey));
                }
            }

            if (Config.IGDB.UseDumps == true && Config.IGDB.DumpsAvailable == true)
            {
                // use dumps if available
                HasheousClient.Models.Metadata.IGDB.Platform[] results = await Metadata.GetObjectsFromDatabase<HasheousClient.Models.Metadata.IGDB.Platform>("platforms", searchFields, searchBody);

                if (results == null || results.Length == 0)
                {
                    return NotFound(new Dictionary<string, string> { { "Error", "No platforms found matching the search criteria." } });
                }

                // store in cache
                if (Config.RedisConfiguration.Enabled)
                {
                    await RedisConnection.SetCacheItem<List<HasheousClient.Models.Metadata.IGDB.Platform>>(cacheKey, results.ToList(), TimeSpan.FromDays(1));
                }

                return Ok(results);
            }
            else
            {
                // use API if dumps are not available
                List<HasheousClient.Models.Metadata.IGDB.Platform>? searchCache = await Communications.GetSearchCache<List<HasheousClient.Models.Metadata.IGDB.Platform>>(searchFields, searchBody);

                if (searchCache == null)
                {
                    // cache miss
                    // get Platform metadata from data source
                    Communications comms = new Communications(Communications.MetadataSources.IGDB);
                    var results = await comms.APIComm<Platform>(IGDBClient.Endpoints.Platforms, searchFields, searchBody);

                    List<HasheousClient.Models.Metadata.IGDB.Platform> platforms = new List<HasheousClient.Models.Metadata.IGDB.Platform>();
                    foreach (Platform platform in results.ToList())
                    {
                        HasheousClient.Models.Metadata.IGDB.Platform tempPlatform = new HasheousClient.Models.Metadata.IGDB.Platform();
                        tempPlatform = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Platform>(platform, tempPlatform);
                        platforms.Add(tempPlatform);
                    }

                    Communications.SetSearchCache<List<HasheousClient.Models.Metadata.IGDB.Platform>>(searchFields, searchBody, platforms);

                    searchCache = platforms;
                }

                // store in cache
                if (Config.RedisConfiguration.Enabled)
                {
                    await RedisConnection.SetCacheItem<List<HasheousClient.Models.Metadata.IGDB.Platform>>(cacheKey, searchCache, TimeSpan.FromDays(1));
                }

                return Ok(searchCache);
            }
        }

        /// <summary>
        /// Search for metadata from IGDB
        /// </summary>
        /// <param name="PlatformId">
        /// The Id of the platform to search games for
        /// </param>
        /// <param name="SearchString">
        /// The string to search for
        /// </param>
        /// <returns>
        /// The game metadata object from IGDB
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("IGDB/Search/Platform/{PlatformId}/Game")]
        [ResponseCache(CacheProfileName = "None")]
        public async Task<IActionResult> SearchMetadata_Game(long PlatformId, string SearchString)
        {
            string searchBody = "";
            string searchFields = "fields *; ";
            searchBody += "search \"" + SearchString + "\";";
            searchBody += "where platforms = (" + PlatformId + ");";
            searchBody += "limit 100;";

            // check cache first
            string cacheKey = RedisConnection.GenerateKey("MetadataProxy-IGDB", "Search-Platform-Game" + PlatformId.ToString() + SearchString);
            if (Config.RedisConfiguration.Enabled)
            {
                if (await RedisConnection.CacheItemExists(cacheKey))
                {
                    return Ok(await RedisConnection.GetCacheItem<List<HasheousClient.Models.Metadata.IGDB.Game>>(cacheKey));
                }
            }

            if (Config.IGDB.UseDumps == true && Config.IGDB.DumpsAvailable == true)
            {
                // use dumps if available
                HasheousClient.Models.Metadata.IGDB.Game[] results = await Metadata.GetObjectsFromDatabase<HasheousClient.Models.Metadata.IGDB.Game>("games", searchFields, searchBody);

                if (results == null || results.Length == 0)
                {
                    return NotFound(new Dictionary<string, string> { { "Error", "No platforms found matching the search criteria." } });
                }

                // store in cache
                if (Config.RedisConfiguration.Enabled)
                {
                    await RedisConnection.SetCacheItem<List<HasheousClient.Models.Metadata.IGDB.Game>>(cacheKey, results.ToList(), TimeSpan.FromDays(1));
                }

                return Ok(results);
            }
            else
            {
                List<HasheousClient.Models.Metadata.IGDB.Game>? searchCache = await Communications.GetSearchCache<List<HasheousClient.Models.Metadata.IGDB.Game>>(searchFields, searchBody);

                if (searchCache == null)
                {
                    // cache miss
                    // get Game metadata from data source
                    Communications comms = new Communications(Communications.MetadataSources.IGDB);
                    var results = await comms.APIComm<Game>(IGDBClient.Endpoints.Games, searchFields, searchBody);

                    List<HasheousClient.Models.Metadata.IGDB.Game> games = new List<HasheousClient.Models.Metadata.IGDB.Game>();
                    foreach (Game game in results.ToList())
                    {
                        HasheousClient.Models.Metadata.IGDB.Game tempGame = new HasheousClient.Models.Metadata.IGDB.Game();
                        tempGame = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Game>(game, tempGame);
                        games.Add(tempGame);
                    }

                    Communications.SetSearchCache<List<HasheousClient.Models.Metadata.IGDB.Game>>(searchFields, searchBody, games);

                    searchCache = games;
                }

                // store in cache
                if (Config.RedisConfiguration.Enabled)
                {
                    await RedisConnection.SetCacheItem<List<HasheousClient.Models.Metadata.IGDB.Game>>(cacheKey, searchCache, TimeSpan.FromDays(1));
                }

                return Ok(searchCache);
            }
        }

        private static HttpClient client = new HttpClient();

        /// <summary>
        /// Get image from IGDB
        /// </summary>
        /// <param name="ImageId">
        /// The Id of the image to fetch
        /// </param>
        /// <returns>
        /// The image from IGDB
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("IGDB/Image/{ImageId}.jpg")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetImage(string ImageId)
        {
            // Validate ImageId to prevent path traversal
            if (ImageId.Contains("..") || ImageId.Contains("/") || ImageId.Contains("\\"))
            {
                return BadRequest("Invalid image ID");
            }

            string resourcePath = $"Images/{ImageId}.jpg";
            string url = String.Format("https://images.igdb.com/igdb/image/upload/t_{0}/{1}.jpg", "original", ImageId);

            try
            {
                // Try to resolve from cache (local or S3 fallback)
                var cachedStream = await ProxyCacheManager.ResolveReadAsync("IGDB", resourcePath, CachePolicyType.Media, "image/jpeg");
                if (cachedStream != null)
                {
                    return FileWithManagedStream(cachedStream, "image/jpeg");
                }

                // Download and cache the image
                var fileStream = await ProxyCacheManager.DownloadAndCacheAsync(url, "IGDB", resourcePath, CachePolicyType.Media, "image/jpeg", HttpContext);
                if (fileStream != null)
                {
                    return FileWithManagedStream(fileStream, "image/jpeg");
                }

                return NotFound();
            }
            catch
            {
                return NotFound();
            }
        }

        #endregion IGDB

        #region TheGamesDB
        /// <summary>
        /// Get image from TheGamesDB
        /// </summary>
        /// <param name="ImageSize">
        /// The size of the image to fetch
        /// </param>
        /// <param name="FileName">
        /// The name of the image to fetch
        /// </param>
        /// <returns></returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("TheGamesDB/Images/{ImageSize}/{*FileName}")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetTheGamesDBImage(MetadataQuery.imageSize ImageSize, string FileName)
        {
            FileName = System.Uri.UnescapeDataString(FileName);
            if (FileName.Contains("..") || FileName.Contains("\\"))
            {
                return BadRequest("Invalid image ID");
            }

            string resourcePath = $"Images/{ImageSize}/{FileName}";
            string url = $"https://cdn.thegamesdb.net/images/{ImageSize}/{FileName}";

            try
            {
                // Try to resolve from cache (local or S3 fallback)
                var cachedStream = await ProxyCacheManager.ResolveReadAsync("TheGamesDB", resourcePath, CachePolicyType.Media, "image/jpeg");
                if (cachedStream != null)
                {
                    return FileWithManagedStream(cachedStream, "image/jpeg");
                }

                // Download and cache the image
                var fileStream = await ProxyCacheManager.DownloadAndCacheAsync(url, "TheGamesDB", resourcePath, CachePolicyType.Media, "image/jpeg", HttpContext);
                if (fileStream != null)
                {
                    return FileWithManagedStream(fileStream, "image/jpeg");
                }

                return NotFound();
            }
            catch
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Get Games by ID from TheGamesDB
        /// </summary>
        /// <param name="id" example="1,2,3" required="true">
        /// A comma-separated list of TheGamesDB Game IDs
        /// </param>
        /// <param name="fields" example="*, players, publishers, genres, overview, last_updated, rating, platform, coop, youtube, os, processor, ram, hdd, video, sound, alternates">
        /// A comma-separated list of fields to return
        /// </param>
        /// <param name="include" example="boxart, platform">
        /// A comma-separated list of fields to include
        /// </param>
        /// <param name="page">
        /// The page number to return
        /// </param>
        /// <param name="pageSize">
        /// The number of results to return per page
        /// </param>
        /// <returns>
        /// The game metadata object from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the game metadata object from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Games/ByGameID")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetGamesByGameID(string id, string fields = "", string include = "", int page = 1, int pageSize = 10)
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                query = id,
                queryField = QueryModel.QueryFieldName.id,
                fieldList = fields,
                includeList = include,
                page = page,
                pageSize = pageSize
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID? games = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID>(queryModel);

            return Ok(games);
        }

        /// <summary>
        /// Get Games by Name from TheGamesDB
        /// </summary>
        /// <param name="name" required="true">
        /// Search term
        /// </param>
        /// <param name="fields" example="*, players, publishers, genres, overview, last_updated, rating, platform, coop, youtube, os, processor, ram, hdd, video, sound, alternates">
        /// A comma-separated list of fields to return
        /// </param>
        /// <param name="filter">
        /// Platform ID to filter by
        /// </param>
        /// <param name="include" example="boxart, platform">
        /// A comma-separated list of fields to include
        /// </param>
        /// <param name="page">
        /// The page number to return
        /// </param>
        /// <param name="pageSize">
        /// The number of results to return per page
        /// </param>
        /// <returns>
        /// The game metadata object from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the game metadata object from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Games/ByGameName")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetGamesByGameName(string name, string fields = "", string filter = "", string include = "", int page = 1, int pageSize = 10)
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                query = name,
                queryField = QueryModel.QueryFieldName.name,
                fieldList = fields,
                filter = filter,
                includeList = include,
                page = page,
                pageSize = pageSize
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID? games = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID>(queryModel);

            return Ok(games);
        }

        /// <summary>
        /// Get Games by platform ID from TheGamesDB
        /// </summary>
        /// <param name="id" example="1" required="true">
        /// A platform ID
        /// </param>
        /// <param name="fields" example="*, players, publishers, genres, overview, last_updated, rating, platform, coop, youtube, os, processor, ram, hdd, video, sound, alternates">
        /// A comma-separated list of fields to return
        /// </param>
        /// <param name="include" example="boxart, platform">
        /// A comma-separated list of fields to include
        /// </param>
        /// <param name="page">
        /// The page number to return
        /// </param>
        /// <param name="pageSize">
        /// The number of results to return per page
        /// </param>
        /// <returns>
        /// The game metadata object from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the game metadata object from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Games/ByPlatformID")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetGamesByPlatformID(string id, string fields = "", string include = "", int page = 1, int pageSize = 10)
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                query = id,
                queryField = QueryModel.QueryFieldName.platform_id,
                fieldList = fields,
                includeList = include,
                page = page,
                pageSize = pageSize
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID? games = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID>(queryModel);

            return Ok(games);
        }

        /// <summary>
        /// Get game images by game ID from TheGamesDB
        /// </summary>
        /// <param name="id" example="1,2,3" required="true">
        /// A comma-separated list of TheGamesDB Game IDs
        /// </param>
        /// <param name="filter" example="fanart, banner, boxart, screenshot, clearlogo, titlescreen">
        /// A comma-separated list of image types to return
        /// </param>
        /// <param name="page">
        /// The page number to return
        /// </param>
        /// <param name="pageSize">
        /// The number of results to return per page
        /// </param>
        /// <returns>
        /// The game images metadata object from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the game images metadata object from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.GamesImages), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Games/Images")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetGamesImages(string id, string filter = "", int page = 1, int pageSize = 10)
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                query = id,
                queryField = QueryModel.QueryFieldName.games_id,
                filter = filter,
                page = page,
                pageSize = pageSize
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.GamesImages? games = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.GamesImages>(queryModel);

            return Ok(games);
        }

        /// <summary>
        /// Get the list of platforms from TheGamesDB
        /// </summary>
        /// <param name="fields" example="icon, console, controller, developer, manufacturer, media, cpu, memory, graphics, sound, maxcontrollers, display, overview, youtube" required="false">
        /// A comma-separated list of fields to return
        /// </param>
        /// <returns>
        /// The platform metadata objects from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the platform metadata objects from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.Platforms), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Platforms")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetPlatforms(string fields = "")
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                fieldList = fields
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.Platforms? platforms = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.Platforms>(queryModel);

            return Ok(platforms);
        }

        /// <summary>
        /// Get the list of platforms by ID from TheGamesDB
        /// </summary>
        /// <param name="id" example="1,2,3" required="true">
        /// A comma-separated list of TheGamesDB Platform IDs
        /// </param>
        /// <param name="fields" example="icon, console, controller, developer, manufacturer, media, cpu, memory, graphics, sound, maxcontrollers, display, overview, youtube" required="false">
        /// A comma-separated list of fields to return
        /// </param>
        /// <returns>
        /// The platform metadata objects from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the platform metadata objects from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformID), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Platforms/ByPlatformID")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetPlatformsByPlatformID(string id, string fields = "")
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                query = id,
                queryField = QueryModel.QueryFieldName.id,
                fieldList = fields
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformID? platforms = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformID>(queryModel);

            return Ok(platforms);
        }

        /// <summary>
        /// Get the list of platforms by name from TheGamesDB
        /// </summary>
        /// <param name="name" required="true">
        /// Search term
        /// </param>
        /// <param name="fields" example="icon, console, controller, developer, manufacturer, media, cpu, memory, graphics, sound, maxcontrollers, display, overview, youtube" required="false">
        /// A comma-separated list of fields to return
        /// </param>
        /// <returns>
        /// The platform metadata objects from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the platform metadata objects from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformName), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Platforms/ByPlatformName")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetPlatformsByPlatformName(string name, string fields = "")
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                query = name,
                queryField = QueryModel.QueryFieldName.platform_name,
                fieldList = fields
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformName? platforms = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.PlatformsByPlatformName>(queryModel);

            return Ok(platforms);
        }

        /// <summary>
        /// Get platform images by platform id from TheGamesDB
        /// </summary>
        /// <param name="id" example="1,2,3" required="true">
        /// A comma-separated list of TheGamesDB Platform IDs
        /// </param>
        /// <param name="filter" example="fanart, banner, boxart">
        /// A comma-separated list of image types to return
        /// </param>
        /// <param name="page">
        /// The page number to return
        /// </param>
        /// <param name="pageSize">
        /// The number of results to return per page
        /// </param>
        /// <returns>
        /// The platform images metadata object from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the platform images metadata object from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        /// <response code="500">If an error occurs</response>
        /// <response code="503">If the service is unavailable</response>
        /// <response code="504">If the service request times out</response>
        /// 
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.PlatformsImages), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Platforms/Images")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetPlatformsImages(string id, string filter = "", int page = 1, int pageSize = 10)
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                query = id,
                queryField = QueryModel.QueryFieldName.platforms_id,
                filter = filter,
                page = page,
                pageSize = pageSize
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.PlatformsImages? platforms = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.PlatformsImages>(queryModel);

            return Ok(platforms);
        }

        /// <summary>
        /// Get the list of Genres from TheGamesDB
        /// </summary>
        /// <returns>
        /// The genre metadata objects from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the genre metadata objects from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.Genres), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Genres")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetGenres()
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {

            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.Genres? genres = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.Genres>(queryModel);

            return Ok(genres);
        }

        /// <summary>
        /// Get the list of Developers from TheGamesDB
        /// </summary>
        /// <returns>
        /// The developer metadata objects from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the developer metadata objects from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.Developers), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Developers")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetDevelopers(int page = 1, int pageSize = 10)
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                page = page,
                pageSize = pageSize
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.Developers? developers = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.Developers>(queryModel);

            return Ok(developers);
        }

        /// <summary>
        /// Get the list of Publishers from TheGamesDB
        /// </summary>
        /// <returns>
        /// The publisher metadata objects from TheGamesDB
        /// </returns>
        /// <response code="200">Returns the publisher metadata objects from TheGamesDB</response>
        /// <response code="400">If the one of the input parameters is bad</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.TheGamesDb.Publishers), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("TheGamesDB/Publishers")]
        // [ResponseCache(CacheProfileName = "7Days")]
        public IActionResult GetPublishers(int page = 1, int pageSize = 10)
        {
            TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
            {
                page = page,
                pageSize = pageSize
            };

            TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
            HasheousClient.Models.Metadata.TheGamesDb.Publishers? publishers = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.Publishers>(queryModel);

            return Ok(publishers);
        }

        #endregion TheGamesDB

        #region GiantBomb
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(GiantBomb.Models.GiantBombGenericResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("GiantBomb/{datatype}/{guid}")]
        public async Task<IActionResult> GetGiantBombResponse_Singular(GiantBomb.MetadataQuery.QueryableTypes datatype, string guid, string? field_list = "*", GiantBomb.MetadataQuery.GiantBombReturnTypes format = GiantBomb.MetadataQuery.GiantBombReturnTypes.json)
        {
            // Search for metadata
            GiantBomb.Models.GiantBombGenericResponse response = GiantBomb.MetadataQuery.GetMetadataByGuid(datatype, guid, field_list);

            if (response.results == null || response.results.Count == 0)
            {
                return NotFound(new Dictionary<string, string> { { "Error", "No items found matching the search criteria." } });
            }

            return FormatOutput(response, format);
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(GiantBomb.Models.GiantBombGenericResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("GiantBomb/{datatype}s")]
        public async Task<IActionResult> GetGiantBombResponse_List(GiantBomb.MetadataQuery.QueryableTypes datatype, string? filter = null, string? sort = null, int limit = 100, int offset = 0, string field_list = "*", GiantBomb.MetadataQuery.GiantBombReturnTypes format = GiantBomb.MetadataQuery.GiantBombReturnTypes.json)
        {
            // Validate input parameters
            if (limit <= 0 || offset < 0)
            {
                return BadRequest("Invalid limit or offset.");
            }

            // Search for metadata
            GiantBomb.Models.GiantBombGenericResponse response = GiantBomb.MetadataQuery.SearchForMetadata(datatype, filter, field_list, sort, limit, offset);

            if (response.results == null || response.results.Count == 0)
            {
                return NotFound(new Dictionary<string, string> { { "Error", "No games found matching the search criteria." } });
            }

            return Ok(response);
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(GiantBomb.Models.GiantBombGenericResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("GiantBomb/companies")]
        public async Task<IActionResult> GetGiantBombResponse_List_CompanyPlural(string? filter = null, string? sort = null, int limit = 100, int offset = 0, string field_list = "*", GiantBomb.MetadataQuery.GiantBombReturnTypes format = GiantBomb.MetadataQuery.GiantBombReturnTypes.json)
        {
            return await GetGiantBombResponse_List(GiantBomb.MetadataQuery.QueryableTypes.company, filter, sort, limit, offset, field_list, format);
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(GiantBomb.Models.GiantBombGenericResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("GiantBomb/images/{guid}")]
        public async Task<IActionResult> GetGiantBombResponse_List_Images(string guid, string? filter = null, string? sort = null, int limit = 100, int offset = 0, string field_list = "*", GiantBomb.MetadataQuery.GiantBombReturnTypes format = GiantBomb.MetadataQuery.GiantBombReturnTypes.json)
        {
            // Validate input parameters
            if (limit <= 0 || offset < 0)
            {
                return BadRequest("Invalid limit or offset.");
            }

            // Search for metadata
            GiantBomb.Models.GiantBombGenericResponse response = GiantBomb.MetadataQuery.SearchForMetadata(GiantBomb.MetadataQuery.QueryableTypes.image, $"guid:{guid},{filter}", field_list, sort, limit, offset);

            if (response.results == null || response.results.Count == 0)
            {
                return NotFound(new Dictionary<string, string> { { "Error", "No games found matching the search criteria." } });
            }

            return Ok(response);
        }

        private IActionResult FormatOutput(GiantBomb.Models.GiantBombGenericResponse response, GiantBomb.MetadataQuery.GiantBombReturnTypes format)
        {
            // return the response in the requested format xml, json, or jsonp
            switch (format)
            {
                case GiantBomb.MetadataQuery.GiantBombReturnTypes.xml:
                    {
                        // Collect concrete result types so XmlSerializer is aware of them (prevents 'type was not expected' errors)
                        var knownTypes = response.results?
                            .Where(r => r != null)
                            .Select(r => r.GetType())
                            .Distinct()
                            .ToArray() ?? Type.EmptyTypes;

                        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(GiantBomb.Models.GiantBombGenericResponse), knownTypes);
                        using var sw = new StringWriter();
                        serializer.Serialize(sw, response);
                        return Content(sw.ToString(), "application/xml");
                    }

                case GiantBomb.MetadataQuery.GiantBombReturnTypes.jsonp:
                    {
                        // Optional callback name (?callback=foo); default to 'callback'
                        var callback = HttpContext.Request.Query.TryGetValue("callback", out var cbVal) && !string.IsNullOrWhiteSpace(cbVal)
                            ? cbVal.ToString()
                            : "callback";

                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(response,
                            new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });

                        var jsonp = $"{callback}({json});";
                        return Content(jsonp, "application/javascript");
                    }

                // json (default) will fall through to existing return Ok(response);
                default:
                    break;
            }

            return Ok(response);
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("GiantBomb/a/uploads/{*GiantBombImagePath}")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetGiantBombImage(string GiantBombImagePath)
        {
            GiantBombImagePath = System.Uri.UnescapeDataString(GiantBombImagePath);
            if (GiantBombImagePath.Contains("..") || GiantBombImagePath.Contains("\\"))
            {
                return BadRequest("Invalid image ID");
            }

            string resourcePath = $"Images/{GiantBombImagePath}";
            string url = $"https://www.giantbomb.com/a/uploads/{GiantBombImagePath}";

            try
            {
                // Try to resolve from cache (local or S3 fallback)
                var cachedStream = await ProxyCacheManager.ResolveReadAsync("GiantBomb", resourcePath, CachePolicyType.Media, "image/jpeg");
                if (cachedStream != null)
                {
                    return FileWithManagedStream(cachedStream, "image/jpeg");
                }

                // Download and cache the image
                var fileStream = await ProxyCacheManager.DownloadAndCacheAsync(url, "GiantBomb", resourcePath, CachePolicyType.Media, "image/jpeg", HttpContext);
                if (fileStream != null)
                {
                    return FileWithManagedStream(fileStream, "image/jpeg");
                }

                return NotFound();
            }
            catch
            {
                return NotFound();
            }
        }
        #endregion GiantBomb

        #region ScreenScraper
        /// <summary>
        /// Get game metadata from ScreenScraper by game ID or checksum (CRC, MD5, SHA1). Returns a response containing game metadata, mirroring the response from the jueInfos.php endpoint of ScreenScraper. If multiple identifiers are provided, the order of precedence is: gameid > md5 > sha1> crc. If no identifiers are provided, a BadRequest response will be returned.
        /// </summary>
        /// <param name="gameid" example="12345" required="false">
        /// The unique identifier of the game in ScreenScraper
        /// </param>
        /// <param name="crc" example="12345678" required="false">
        /// The CRC checksum of the game file
        /// </param>
        /// <param name="md5" example="d41d8cd98f00204e9800998ecf8427e" required="false">
        /// The MD5 checksum of the game file
        /// </param>
        /// <param name="sha1" example="da39a3ee5e6b4b0d3255bfef95601890afd80709" required="false">
        /// The SHA1 checksum of the game file
        /// </param>
        /// <param name="output" example="json" required="false">
        /// The output format of the response: "json" or "xml". Default is "json".
        /// </param>
        /// <returns>
        /// A JSON or XML object containing game metadata from ScreenScraper. Note: the servers and ssuser attributes will be blanked out for security reasons, and the attributes may not be included in the result.
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(hasheous_server.Classes.MetadataLib.MetadataScreenScraper.GameItem), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("ScreenScraper/jeuInfos.php")]
        public async Task<IActionResult> GetScreenScraperResponse_Singular(long? gameid, string? crc, string? md5, string? sha1, string? output = "json")
        {
            if (gameid == null && string.IsNullOrEmpty(crc) && string.IsNullOrEmpty(md5) && string.IsNullOrEmpty(sha1))
            {
                return BadRequest("At least one identifier (gameid, crc, md5, sha1) must be provided.");
            }

            if (gameid == null)
            {
                // search by checksum
                hasheous_server.Models.HashLookupModel hashLookupModel = new hasheous_server.Models.HashLookupModel();
                if (!string.IsNullOrEmpty(md5))
                {
                    hashLookupModel.MD5 = md5;
                }
                if (!string.IsNullOrEmpty(sha1))
                {
                    hashLookupModel.SHA1 = sha1;
                }
                if (!string.IsNullOrEmpty(crc))
                {
                    hashLookupModel.CRC = crc;
                }

                HashLookup hashLookup = new HashLookup(new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString), new List<hasheous_server.Models.HashLookupModel> { hashLookupModel }, false, "metadata", null);
                await hashLookup.PerformLookup(true);

                if (hashLookup != null && hashLookup.Metadata != null && hashLookup.Metadata.Count > 0)
                {
                    // find the screenscraper gameid in the metadata attribute
                    var metadataItem = hashLookup.Metadata.Find(m => m.Source == Communications.MetadataSources.ScreenScraper);
                    if (metadataItem != null && !string.IsNullOrEmpty(metadataItem.ImmutableId))
                    {
                        gameid = long.Parse(metadataItem.ImmutableId);
                    }
                    else
                    {
                        return NotFound("No game found for the provided checksum(s).");
                    }
                }
                else
                {
                    return NotFound("No game found for the provided checksum(s).");
                }
            }

            // Now that we have a gameid, fetch the metadata from the cached files
            string cacheFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_Screenscraper, "games", $"{gameid}.json");
            if (!System.IO.File.Exists(cacheFilePath))
            {
                // if the cache file doesn't exist, try to fetch the metadata from the ScreenScraper API and cache it
                string endpointUrl = Classes.MetadataLib.MetadataScreenScraper.GameItem.Endpoint((long)gameid);
                var lookupMatchItem = await Classes.MetadataLib.MetadataScreenScraper.DownloadFromApi(endpointUrl, null, new Dictionary<string, object>());

                if (lookupMatchItem == null)
                {
                    return NotFound("No metadata found for the provided gameid.");
                }
            }

            string jsonContent = await System.IO.File.ReadAllTextAsync(cacheFilePath);
            var gameItem = Newtonsoft.Json.JsonConvert.DeserializeObject<hasheous_server.Classes.MetadataLib.MetadataScreenScraper.ssGame>(jsonContent);

            if (gameItem == null)
            {
                return NotFound("Failed to deserialize metadata for the provided gameid.");
            }

            // strip all media urls of login details since screenscraper requires credentials in the url, and we don't want to expose that in the response
            foreach (var media in gameItem.medias)
            {
                if (!string.IsNullOrEmpty(media.url))
                {
                    string endpointUrl = Classes.MetadataLib.MetadataScreenScraper.ssMedia.Endpoint((long)gameItem.id, long.Parse(gameItem.systeme.id), media.type, media.region);

                    // rewrite the media URL to point to the local cache instead of the original URL
#if DEBUG
                    media.originalUrl = endpointUrl; // store the original URL for reference
#endif

                    // blank security credentials from the URL: credentials query string attributes are named: devid, devpassword, ssid, sspassword
                    var uriBuilder = new UriBuilder(endpointUrl);
                    var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                    query.Remove("devid"); // blank out the value
                    query.Remove("devpassword"); // blank out the value
                    query.Remove("ssid"); // blank out the value
                    query.Remove("sspassword"); // blank out the value
                    query.Remove("softname"); // blank out the value
                    uriBuilder.Query = query.ToString();
                    media.url = uriBuilder.ToString();
                    media.url = media.url.Replace("https://api.screenscraper.fr/api2/", "/api/v1/MetadataProxy/ScreenScraper/").Replace("https://api.screenscraper.fr:443/api2/", "/api/v1/MetadataProxy/ScreenScraper/"); // rewrite the URL to point to the local proxy endpoint
                }
            }

            // format for response to match the ScreenScraper API structure
            var response = new hasheous_server.Classes.MetadataLib.MetadataScreenScraper.GameItem
            {
                header = new hasheous_server.Classes.MetadataLib.MetadataScreenScraper.ssHeader
                {
                    APIversion = "1.0",
                    dateTime = DateTime.UtcNow,
                    commandRequested = "jeuInfos.php",
                    success = true
                },
                response = new hasheous_server.Classes.MetadataLib.MetadataScreenScraper.GameItem.GameInfoResponse
                {
                    jeu = gameItem
                }
            };

            // If the output format is XML, serialize the response to XML
            if (output?.ToLower() == "xml")
            {
                var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(hasheous_server.Classes.MetadataLib.MetadataScreenScraper.GameItem));
                using var stringWriter = new StringWriter();
                xmlSerializer.Serialize(stringWriter, response);
                return Content(stringWriter.ToString(), "application/xml");
            }

            // Default to JSON response
            return Ok(response);
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("ScreenScraper/systemesListe.php")]
        public async Task<IActionResult> GetScreenScraperPlatforms()
        {
            var screenScraper = new Classes.MetadataLib.MetadataScreenScraper();
            var platforms = await screenScraper.GetPlatformsAsync();
            return Ok(platforms);
        }

        /// <summary>
        /// Get image from ScreenScraper
        /// </summary>
        /// <param name="endpoint" example="Jeu" required="true">
        /// The endpoint to retrieve the image from (e.g., "Jeu", "JeuMedia")
        /// </param>
        /// <param name="systemeid" example="1" required="true">
        /// The unique identifier of the system in ScreenScraper
        /// </param>
        /// <param name="jeuid" example="12345" required="true">
        /// The unique identifier of the game in ScreenScraper
        /// </param>
        /// <param name="media" example="boxart" required="true">
        /// The type of media to retrieve (e.g., "boxart", "screenshot", "fanart")
        /// </param>
        /// <returns>
        /// The image from ScreenScraper
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("ScreenScraper/media{endpoint}.php")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetScreenScraperMedia(string endpoint, long systemeid, long jeuid, string media)
        {
            // Validate media to prevent path traversal
            if (media.Contains("..") || media.Contains("/") || media.Contains("\\"))
            {
                return BadRequest("Invalid media ID");
            }

            // get the record
            hasheous_server.Classes.MetadataLib.MetadataScreenScraper.ssGame? gameItem = null;
            IActionResult gameDataResult = await GetScreenScraperResponse_Singular(jeuid, null, null, null, "json");
            if (gameDataResult is OkObjectResult ok && ok.Value is hasheous_server.Classes.MetadataLib.MetadataScreenScraper.GameItem payload)
            {
                gameItem = payload.response?.jeu;
                // use gameItem.medias here
            }
            else if (gameDataResult is ObjectResult obj && obj.Value != null)
            {
                // fallback if needed
            }
            else
            {
                return NotFound("Game metadata not found.");
            }

            // split media name into type and region (if applicable)
            string mediaType = media;
            string? mediaRegion = null;
            if (media.Contains("("))
            {
                int regionStartIndex = media.IndexOf("(");
                int regionEndIndex = media.IndexOf(")");
                if (regionStartIndex >= 0 && regionEndIndex > regionStartIndex)
                {
                    mediaType = media.Substring(0, regionStartIndex);
                    mediaRegion = media.Substring(regionStartIndex + 1, regionEndIndex - regionStartIndex - 1);
                }
            }

            // find the associated media item in the gameItem.medias list
            var mediaItem = gameItem?.medias?.FirstOrDefault(m => m.type == mediaType && (mediaRegion == null || m.region == mediaRegion));
            if (mediaItem == null)
            {
                return NotFound("Media not found for the specified game and system.");
            }

            string mimeType = "image/png";
            string extension = "jpg"; // default extension
            switch (mediaItem.format?.ToLower())
            {
                case "jpg":
                case "jpeg":
                    mimeType = "image/jpeg";
                    extension = "jpg";
                    break;
                case "gif":
                    mimeType = "image/gif";
                    extension = "gif";
                    break;
                case "bmp":
                    mimeType = "image/bmp";
                    extension = "bmp";
                    break;
                case "tiff":
                    mimeType = "image/tiff";
                    extension = "tiff";
                    break;
                case "pdf":
                    mimeType = "application/pdf";
                    extension = "pdf";
                    break;
                case "mp4":
                    mimeType = "video/mp4";
                    extension = "mp4";
                    break;
                case "svg":
                    mimeType = "image/svg+xml";
                    extension = "svg";
                    break;
                default:
                    mimeType = "image/png";
                    extension = "png";
                    break;
            }

            string resourcePath = $"Images/{systemeid}/{jeuid}/{media}.{extension}";
            string url = Classes.MetadataLib.MetadataScreenScraper.ssMedia.Endpoint(jeuid, systemeid, media, null);

            try
            {
                // Try to resolve from cache (local or S3 fallback)
                var cachedStream = await ProxyCacheManager.ResolveReadAsync("Screenscraper", resourcePath, CachePolicyType.Media, mimeType);
                if (cachedStream != null)
                {
                    return FileWithManagedStream(cachedStream, mimeType);
                }

                // Download and cache the image
                var fileStream = await ProxyCacheManager.DownloadAndCacheAsync(url, "Screenscraper", resourcePath, CachePolicyType.Media, mimeType, HttpContext);
                if (fileStream != null)
                {
                    return FileWithManagedStream(fileStream, mimeType);
                }

                return NotFound();
            }
            catch
            {
                return NotFound();
            }
        }
        #endregion ScreenScraper

        #region MetadataBundles
        /// <summary>
        /// Get a metadata bundle by its ID. Bundles contain pre-packaged metadata and images for offline use.
        /// </summary>
        /// <param name="MetadataSourceName" example="IGDB" required="true">
        /// The name of the metadata source (e.g., IGDB, TheGamesDB, GiantBomb)
        /// </param>
        /// <param name="GameID" example="12345" required="true">
        /// The unique identifier of the game within the specified metadata source
        /// </param>
        /// <returns>
        /// A metadata bundle file for the specified game
        /// </returns>
        /// <response code="200">Returns the metadata bundle file for the specified game</response>
        /// <response code="400">If the MetadataSourceName is invalid</response>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(typeof(PhysicalFileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Route("Bundles/{MetadataSourceName}/{GameID}.bundle")]
        public async Task<IActionResult> GetMetadataBundle(string MetadataSourceName, string GameID)
        {
            // validate GameID
            GameID = System.Uri.UnescapeDataString(GameID);
            if (GameID.Contains("..") || GameID.Contains("\\"))
            {
                return BadRequest("Invalid game ID");
            }
            else if (GameID.Contains("/"))
            {
                return BadRequest("Invalid game ID");
            }

            // validate MetadataSourceName
            var validSources = new List<string> { "IGDB", "TheGamesDB", "GiantBomb" };
            if (!validSources.Contains(MetadataSourceName))
            {
                return BadRequest("Invalid metadata source");
            }

            // check the disk for a pre-built bundle
            if (!Directory.Exists(Config.LibraryConfiguration.LibraryMetadataBundlesDirectory))
            {
                Directory.CreateDirectory(Config.LibraryConfiguration.LibraryMetadataBundlesDirectory);
            }
            string fileName = $"{MetadataSourceName}_{GameID}.bundle";
            FileInfo? fileInfo;
            string bundleFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataBundlesDirectory, fileName);
            string resourcePath = fileName;
            bool buildNewBundle = true;

            if (System.IO.File.Exists(bundleFilePath))
            {
                // check the file age
                fileInfo = new FileInfo(bundleFilePath);
                if ((DateTime.Now - fileInfo.LastWriteTime).TotalDays <= Config.MetadataConfiguration.MetadataBundle_MaxAgeInDays)
                {
                    // Try to resolve from cache (local or S3 fallback)
                    var cachedStream = await ProxyCacheManager.ResolveReadAsync("Bundles", resourcePath, CachePolicyType.Bundles, "application/octet-stream");
                    if (cachedStream != null)
                    {
                        return FileWithManagedStream(cachedStream, "application/octet-stream", fileName);
                    }

                    // Rebuild if the existing bundle cannot be served.
                    buildNewBundle = true;
                }
            }

            if (buildNewBundle && !System.IO.File.Exists(bundleFilePath))
            {
                // Try to resolve from cache (local or S3 fallback)
                var cachedStream = await ProxyCacheManager.ResolveReadAsync("Bundles", resourcePath, CachePolicyType.Bundles, "application/octet-stream");
                if (cachedStream != null)
                {
                    return FileWithManagedStream(cachedStream, "application/octet-stream", fileName);
                }
            }

            // build a new bundle
            if (buildNewBundle)
            {
                // create a temporary working directory
                string tempWorkingDir = Path.Combine(Config.LibraryConfiguration.LibraryTemporaryBundlesDirectory, Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempWorkingDir);

                // get the root game metadata based on GameID
                switch (MetadataSourceName)
                {
                    case "IGDB":
                        string[] imageProperties = new string[]
                        {
                            "artworks",
                            "cover",
                            "screenshots"
                        };

                        var igdbGameData = await GetMetadata("Game", long.Parse(GameID), "", "*");
                        // extract the json response
                        string? igdbGame = null;
                        Dictionary<string, object>? igdbGameObj = null;
                        if (igdbGameData is OkObjectResult okResult)
                        {
                            igdbGameObj = okResult.Value as Dictionary<string, object>;
                            igdbGame = Newtonsoft.Json.JsonConvert.SerializeObject(igdbGameObj, Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.JsonSerializerSettings
                            {
                                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                MaxDepth = 64
                            });
                        }
                        else
                        {
                            return BadRequest();
                        }

                        if (igdbGameData == null || igdbGame == null)
                        {
                            return NotFound();
                        }

                        // build the bundle
                        // drop the game metadata json file
                        await _AddMetadataToBundle(tempWorkingDir, "Game", igdbGame);

                        // start adding images
                        foreach (string imageProperty in imageProperties)
                        {
                            bool singleItemCheckCompleted = false;
                            bool isSingleItem = true;

                            if (igdbGameObj != null && igdbGameObj.ContainsKey(imageProperty))
                            {
                                // check if the property is a list
                                if (igdbGameObj[imageProperty] is System.Collections.IEnumerable enumerable &&
                                    !(igdbGameObj[imageProperty] is string))
                                {
                                    // check if the enumerable is a single item
                                    if (singleItemCheckCompleted == false)
                                    {
                                        // if the key for the first item is a long, it's a list
                                        var enumerator = enumerable.GetEnumerator();
                                        if (enumerator.MoveNext())
                                        {
                                            var firstItem = enumerator.Current;
                                            if (firstItem is KeyValuePair<string, object> kvp)
                                            {
                                                if (long.TryParse(kvp.Key, out long _))
                                                {
                                                    isSingleItem = false;
                                                }
                                            }
                                        }

                                        singleItemCheckCompleted = true;
                                    }

                                    if (isSingleItem == true)
                                    {
                                        // it's a single item - iterate over until we find the image_id
                                        foreach (KeyValuePair<string, object> item in enumerable)
                                        {
                                            // item is a keyvaluepair<string, object> - the value is a Dictionary<string, object>
                                            if (item.Key == "image_id")
                                            {
                                                string imageID = item.Value.ToString() ?? "";
                                                if (await GetImage(imageID) is FileResult imageFileData)
                                                {
                                                    await _AddFileToBundle(tempWorkingDir, imageProperty, imageFileData, imageID + ".jpg");
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        foreach (KeyValuePair<string, object> item in enumerable)
                                        {
                                            // item is a keyvaluepair<string, object> - the value is a Dictionary<string, object>
                                            var imageObj = item.Value as Dictionary<string, object>;
                                            if (imageObj != null && imageObj.ContainsKey("image_id"))
                                            {
                                                string imageID = imageObj["image_id"].ToString() ?? "";
                                                if (await GetImage(imageID) is FileResult imageFileData)
                                                {
                                                    await _AddFileToBundle(tempWorkingDir, imageProperty, imageFileData, imageID + ".jpg");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        break;

                    case "TheGamesDB":
                        var tgdbGameData = GetGamesByGameID(GameID, "*, players, publishers, genres, overview, last_updated, rating, platform, coop, youtube, os, processor, ram, hdd, video, sound, alternates", "boxart, platform", 1, 10);
                        // extract the json response
                        string? tgdbGame = null;
                        HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID? tgdbGameObj = null;
                        if (tgdbGameData is OkObjectResult okResult2)
                        {
                            tgdbGameObj = okResult2.Value as HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID;
                            tgdbGame = Newtonsoft.Json.JsonConvert.SerializeObject(tgdbGameObj, Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.JsonSerializerSettings
                            {
                                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                MaxDepth = 64
                            });
                        }
                        else
                        {
                            return BadRequest();
                        }
                        if (tgdbGameData == null || tgdbGame == null)
                        {
                            return NotFound();
                        }

                        // build the bundle
                        // drop the game metadata json file
                        await _AddMetadataToBundle(tempWorkingDir, "Game", tgdbGame);

                        // start adding images
                        if (tgdbGameObj != null && tgdbGameObj.include != null && tgdbGameObj.include.boxart != null && tgdbGameObj.include.boxart.data != null)
                        {
                            foreach (var imageKv in tgdbGameObj.include.boxart.data)
                            {
                                var imageObj = imageKv.Value;
                                foreach (var image in imageObj)
                                {
                                    string imagePath = image.filename ?? "";
                                    if (await GetTheGamesDBImage(MetadataQuery.imageSize.original, imagePath) is FileResult imageFileData)
                                    {
                                        await _AddFileToBundle(tempWorkingDir, image.type, imageFileData, Path.GetFileName(imagePath));
                                    }
                                }
                            }
                        }

                        break;
                }

                // zip the bundle
                if (System.IO.File.Exists(bundleFilePath))
                {
                    System.IO.File.Delete(bundleFilePath);
                }

                ZipFile.CreateFromDirectory(tempWorkingDir, bundleFilePath, CompressionLevel.Fastest, false);

                // Schedule S3 upload via response completion (non-blocking)
                if (Config.S3StorageConfiguration.Enabled)
                {
                    try
                    {
                        HttpContext.Response.OnCompleted(async () =>
                        {
                            try
                            {
                                StorageFallbackResolver resolver = new StorageFallbackResolver();
                                await resolver.UploadLocalFileToS3Async(bundleFilePath, Config.S3StorageConfiguration.DefaultBucket, $"Bundles/{fileName}", overwrite: false);
                            }
                            catch (Exception ex)
                            {
                                Logging.Log(Logging.LogType.Warning, "MetadataProxyController", $"S3 bundle upload failed for {fileName}: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "MetadataProxyController", $"Failed to schedule S3 bundle upload: {ex.Message}");
                    }
                }

                // clean up the temporary working directory
                Directory.Delete(tempWorkingDir, true);
            }

            fileInfo = new FileInfo(bundleFilePath);
            return PhysicalFile(bundleFilePath, "application/octet-stream", fileName, fileInfo.LastWriteTimeUtc, new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{fileInfo.Length}-{fileInfo.LastWriteTimeUtc.Ticks}\""));
        }

        private async Task _AddMetadataToBundle(string tempBundleDir, string metadataType, string metadata)
        {
            // create a sub-directory for the metadata type
            if (Directory.Exists(tempBundleDir) == false)
            {
                Directory.CreateDirectory(tempBundleDir);
            }

            // write the metadata json to a file
            string metadataFilePath = Path.Combine(tempBundleDir, $"{metadataType}.json");
            await System.IO.File.WriteAllTextAsync(metadataFilePath, metadata);
        }

        private async Task _AddFileToBundle(string tempBundleDir, string relativePathInBundle, FileResult fileData, string fileNameHint)
        {
            // create the directory if it doesn't exist
            string fileDir = Path.Combine(tempBundleDir, relativePathInBundle);

            if (!Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }

            string destFileName = Path.GetFileName(fileNameHint);
            if (string.IsNullOrWhiteSpace(destFileName))
            {
                destFileName = Guid.NewGuid().ToString("N");
            }

            string filePathInBundle = Path.Combine(fileDir, destFileName);

            if (fileData is PhysicalFileResult physicalFile)
            {
                string? sourcePath = physicalFile.FileName;
                if (string.IsNullOrWhiteSpace(sourcePath) || !System.IO.File.Exists(sourcePath))
                {
                    throw new FileNotFoundException("Source file for bundle not found.", sourcePath ?? "<null>");
                }

                // Avoid copying a file onto itself
                if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(filePathInBundle), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // copy the file data to the bundle
                // Use explicit streams with read sharing to minimize "file in use" issues
                using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var destStream = new FileStream(filePathInBundle, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await sourceStream.CopyToAsync(destStream);
                }

                return;
            }

            if (fileData is FileStreamResult streamResult)
            {
                Stream sourceStream = streamResult.FileStream;
                if (sourceStream.CanSeek)
                {
                    sourceStream.Position = 0;
                }

                await using (var destStream = new FileStream(filePathInBundle, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await sourceStream.CopyToAsync(destStream);
                }

                return;
            }

            if (fileData is FileContentResult contentResult)
            {
                await System.IO.File.WriteAllBytesAsync(filePathInBundle, contentResult.FileContents);
                return;
            }

            throw new InvalidOperationException($"Unsupported file result type for bundle input: {fileData.GetType().Name}");
        }

        #endregion MetadataBundles
    }
}