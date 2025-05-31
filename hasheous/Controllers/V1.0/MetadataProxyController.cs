using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Web;
using Classes;
using Classes.Metadata;
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
    /// Endpoints used for proxying metadata from IGDB
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [ClientApiKey()]
    public class MetadataProxyController : ControllerBase
    {
        /// <summary>
        /// Get metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the metadata object to fetch
        /// </param>
        /// <param name="slug">
        /// The slug of the metadata object to fetch - note that this is optional and not all metadata types have slugs.
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
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/{MetadataType}")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata(string MetadataType, long Id, string slug = "")
        {
            // check that MetadataType is a valid class in IGDB.Models
            var igdbAssembly = typeof(IGDB.Models.Game).Assembly;
            Type? metadataType = igdbAssembly.GetType($"IGDB.Models.{MetadataType}", false, true);
            if (metadataType == null)
            {
                return BadRequest(new Dictionary<string, string> { { "Error", $"Metadata type '{MetadataType}' not found in IGDB.Models namespace." } });
            }

            // If valid, continue with your logic (e.g., call _GetMetadata)
            return await _GetMetadata(MetadataType, Id, slug);
        }

        private async Task<IActionResult> _GetMetadata(string routeName, long Id, string slug = "")
        {
            // reject invalid id or slug
            if (Id == 0 && slug == "")
            {
                return BadRequest();
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

            if (returnValue != null)
            {
                return Ok(returnValue);
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

            return Ok(searchCache);
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

            return Ok(searchCache);
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
        public IActionResult GetTheGamesDBImage(MetadataQuery.imageSize ImageSize, string FileName)
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

    }
}