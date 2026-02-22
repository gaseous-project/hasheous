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

            string imageDirectory = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB, "Images");
            string imagePath = Path.Combine(imageDirectory, ImageId + ".jpg");

            // create directory if it doesn't exist
            if (!System.IO.Directory.Exists(imageDirectory))
            {
                System.IO.Directory.CreateDirectory(imageDirectory);
            }

            // check if image exists
            if (System.IO.File.Exists(imagePath))
            {
                // check the file is non-zero length
                FileInfo fileInfo = new FileInfo(imagePath);
                if (fileInfo.Length == 0)
                {
                    System.IO.File.Delete(imagePath);
                    return NotFound();
                }

                return PhysicalFile(imagePath, "image/jpeg");
            }
            else
            {
                // get image from IGDB
                string url = String.Format("https://images.igdb.com/igdb/image/upload/t_{0}/{1}.jpg", "original", ImageId);

                // download image from url
                try
                {
                    using (HttpResponseMessage response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                    {
                        response.EnsureSuccessStatusCode();

                        using (Stream contentStream = await response.Content.ReadAsStreamAsync(), fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var totalRead = 0L;
                            var totalReads = 0L;
                            var buffer = new byte[8192];
                            var isMoreToRead = true;

                            do
                            {
                                var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                                if (read == 0)
                                {
                                    isMoreToRead = false;
                                }
                                else
                                {
                                    await fileStream.WriteAsync(buffer, 0, read);

                                    totalRead += read;
                                    totalReads += 1;

                                    if (totalReads % 2000 == 0)
                                    {
                                        Console.WriteLine(string.Format("total bytes downloaded so far: {0:n0}", totalRead));
                                    }
                                }
                            }
                            while (isMoreToRead);
                        }
                    }

                    if (System.IO.File.Exists(imagePath))
                    {
                        // check the file is non-zero length
                        FileInfo fileInfo = new FileInfo(imagePath);
                        if (fileInfo.Length == 0)
                        {
                            System.IO.File.Delete(imagePath);
                            return NotFound();
                        }
                    }

                    return PhysicalFile(imagePath, "image/jpeg");
                }
                catch (Exception ex)
                {
                    return NotFound();
                }
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
            else if (FileName.Contains("/"))
            {
                // forward slashes are allowed in the file name
            }
            string imageFile = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_TheGamesDb, "Images", ImageSize.ToString(), FileName);
            string imagePath = Path.GetDirectoryName(imageFile);

            // create directory if it doesn't exist
            if (!Directory.Exists(imagePath))
            {
                Directory.CreateDirectory(imagePath);
            }

            // check if image exists
            if (System.IO.File.Exists(imageFile))
            {
                // check the file is non-zero length
                FileInfo fileInfo = new FileInfo(imageFile);
                if (fileInfo.Length == 0)
                {
                    System.IO.File.Delete(imageFile);
                    return NotFound();
                }

                return PhysicalFile(imageFile, "image/jpeg");
            }
            else
            {
                // download image from url
                try
                {
                    Uri theGamesDBUri = new Uri("https://cdn.thegamesdb.net/images/" + ImageSize.ToString() + "/" + FileName);

                    DownloadManager downloadManager = new DownloadManager();
                    var result = downloadManager.DownloadFile(theGamesDBUri.ToString(), imageFile);

                    // wait until result is completed
                    while (result.IsCompleted == false)
                    {
                        Thread.Sleep(1000);
                    }

                    // check the file is non-zero length
                    FileInfo fileInfo = new FileInfo(imageFile);
                    if (fileInfo.Length == 0)
                    {
                        System.IO.File.Delete(imageFile);
                        return NotFound();
                    }

                    return PhysicalFile(imageFile, "image/jpeg");
                }
                catch (Exception ex)
                {
                    return NotFound();
                }
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
        public IActionResult GetGiantBombImage(string GiantBombImagePath)
        {
            GiantBombImagePath = System.Uri.UnescapeDataString(GiantBombImagePath);
            if (GiantBombImagePath.Contains("..") || GiantBombImagePath.Contains("\\"))
            {
                return BadRequest("Invalid image ID");
            }
            else if (GiantBombImagePath.Contains("/"))
            {
                // forward slashes are allowed in the file name
            }
            string imageFile = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_GiantBomb, "Images", GiantBombImagePath);
            string imagePath = Path.GetDirectoryName(imageFile);

            // create directory if it doesn't exist
            if (!Directory.Exists(imagePath))
            {
                Directory.CreateDirectory(imagePath);
            }

            // check if image exists
            if (System.IO.File.Exists(imageFile))
            {
                // check the file is non-zero length
                FileInfo fileInfo = new FileInfo(imageFile);
                if (fileInfo.Length == 0)
                {
                    System.IO.File.Delete(imageFile);
                    return NotFound();
                }

                return PhysicalFile(imageFile, "image/jpeg");
            }
            else
            {
                // download image from url
                try
                {
                    Uri giantBombUri = new Uri("https://www.giantbomb.com/a/uploads/" + GiantBombImagePath);

                    DownloadManager downloadManager = new DownloadManager();
                    var result = downloadManager.DownloadFile(giantBombUri.ToString(), imageFile);

                    // wait until result is completed
                    while (result.IsCompleted == false)
                    {
                        Thread.Sleep(1000);
                    }

                    // check the file is non-zero length
                    FileInfo fileInfo = new FileInfo(imageFile);
                    if (fileInfo.Length == 0)
                    {
                        System.IO.File.Delete(imageFile);
                        return NotFound();
                    }

                    return PhysicalFile(imageFile, "image/jpeg");
                }
                catch (Exception ex)
                {
                    return NotFound();
                }
            }
        }
        #endregion GiantBomb

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
            bool buildNewBundle = true;
            if (System.IO.File.Exists(bundleFilePath))
            {
                // check the file age
                fileInfo = new FileInfo(bundleFilePath);
                if ((DateTime.Now - fileInfo.LastWriteTime).TotalDays <= Config.MetadataConfiguration.MetadataBundle_MaxAgeInDays)
                {
                    // return the existing bundle
                    buildNewBundle = false;
                }
            }

            // build a new bundle
            if (buildNewBundle)
            {
                // create a temporary working directory
                string tempWorkingDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
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
                                                PhysicalFileResult? imageFileData = (PhysicalFileResult?)await GetImage(imageID);
                                                if (imageFileData != null)
                                                {
                                                    await _AddFileToBundle(tempWorkingDir, imageProperty, imageFileData);
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
                                                PhysicalFileResult? imageFileData = (PhysicalFileResult?)await GetImage(imageID);
                                                if (imageFileData != null)
                                                {
                                                    await _AddFileToBundle(tempWorkingDir, imageProperty, imageFileData);
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
                                    PhysicalFileResult? imageFileData = (PhysicalFileResult?)await GetTheGamesDBImage(MetadataQuery.imageSize.original, imagePath);
                                    if (imageFileData != null)
                                    {
                                        await _AddFileToBundle(tempWorkingDir, image.type, imageFileData);
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

        private async Task _AddFileToBundle(string tempBundleDir, string relativePathInBundle, PhysicalFileResult fileData)
        {
            // create the directory if it doesn't exist
            string fileDir = Path.Combine(tempBundleDir, relativePathInBundle);

            if (!Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }

            // PhysicalFileResult provides a physical path via FileName; copy from disk
            string? sourcePath = fileData.FileName;
            // Only use the basename for the bundle, not the full source path
            string destFileName = Path.GetFileName(sourcePath ?? string.Empty);
            string filePathInBundle = Path.Combine(fileDir, destFileName);
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
        }

        #endregion MetadataBundles
    }
}