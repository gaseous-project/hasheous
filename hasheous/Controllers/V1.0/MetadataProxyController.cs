using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using Classes;
using Classes.Metadata;
using hasheous_server.Classes.Metadata;
using hasheous_server.Classes.Metadata.IGDB;
using IGDB;
using IGDB.Models;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Endpoints used for proxying metadata from IGDB
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    public class MetadataProxyController : ControllerBase
    {
        /// <summary>
        /// Get metadata from IGDB
        /// </summary>
        /// <param name="Id">The Id of the IGDB object to fetch</param>
        /// <returns>
        /// The metadata object from IGDB
        /// </returns>
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Route("IGDB/AgeRating")]
        [Route("IGDB/AgeRatingContentDescription")]
        [Route("IGDB/AlternativeName")]
        [Route("IGDB/Artwork")]
        [Route("IGDB/Collection")]
        [Route("IGDB/Company")]
        [Route("IGDB/CompanyLogo")]
        [Route("IGDB/Cover")]
        [Route("IGDB/ExternalGame")]
        [Route("IGDB/Franchise")]
        [Route("IGDB/GameMode")]
        [Route("IGDB/Game")]
        [Route("IGDB/GameVideo")]
        [Route("IGDB/Genre")]
        [Route("IGDB/InvolvedCompany")]
        [Route("IGDB/MultiplayerMode")]
        [Route("IGDB/PlatformLogo")]
        [Route("IGDB/Platform")]
        [Route("IGDB/PlatformVersion")]
        [Route("IGDB/PlayerPerspective")]
        [Route("IGDB/ReleaseDate")]
        [Route("IGDB/Screenshot")]
        [Route("IGDB/Theme")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata(long Id)
        {
            // get last value of Request.Path.Value
            string[] path = Request.Path.Value.Split("/");
            string lastValue = path[path.Length - 1];

            // get metadata
            switch (lastValue)
            {
                case "AgeRating":
                    AgeRating? ageRating = AgeRatings.GetAgeRatings(Id);
                    if (ageRating != null)
                    {
                        return Ok(ageRating);
                    }
                    break;

                case "AgeRatingContentDescription":
                    AgeRatingContentDescription? ageRatingContentDescription = AgeRatingContentDescriptions.GetAgeRatingContentDescriptions(Id);
                    if (ageRatingContentDescription != null)
                    {
                        return Ok(ageRatingContentDescription);
                    }
                    break;

                case "AlternativeName":
                    AlternativeName? alternativeName = AlternativeNames.GetAlternativeNames(Id);
                    if (alternativeName != null)
                    {
                        return Ok(alternativeName);
                    }
                    break;

                case "Artwork":
                    Artwork? artwork = Artworks.GetArtwork(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (artwork != null)
                    {
                        return Ok(artwork);
                    }
                    break;

                case "Collection":
                    Collection? collection = Collections.GetCollections(Id);
                    if (collection != null)
                    {
                        return Ok(collection);
                    }
                    break;

                case "Company":
                    Company? company = Companies.GetCompanies(Id);
                    if (company != null)
                    {
                        return Ok(company);
                    }
                    break;

                case "CompanyLogo":
                    CompanyLogo? companyLogo = CompanyLogos.GetCompanyLogo(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (companyLogo != null)
                    {
                        return Ok(companyLogo);
                    }
                    break;

                case "Cover":
                    Cover? cover = Covers.GetCover(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (cover != null)
                    {
                        return Ok(cover);
                    }
                    break;

                case "ExternalGame":
                    ExternalGame? externalGame = ExternalGames.GetExternalGames(Id);
                    if (externalGame != null)
                    {
                        return Ok(externalGame);
                    }
                    break;

                case "Franchise":
                    Franchise? franchise = Franchises.GetFranchises(Id);
                    if (franchise != null)
                    {
                        return Ok(franchise);
                    }
                    break;

                case "GameMode":
                    GameMode? gameMode = GameModes.GetGame_Modes(Id);
                    if (gameMode != null)
                    {
                        return Ok(gameMode);
                    }
                    break;

                case "Game":
                    Game? game = Games.GetGame(Id, false, false, false);
                    if (game != null)
                    {
                        return Ok(game);
                    }
                    break;

                case "GameVideo":
                    GameVideo? gameVideo = GamesVideos.GetGame_Videos(Id);
                    if (gameVideo != null)
                    {
                        return Ok(gameVideo);
                    }
                    break;

                case "Genre":
                    Genre? genre = Genres.GetGenres(Id);
                    if (genre != null)
                    {
                        return Ok(genre);
                    }
                    break;

                case "InvolvedCompany":
                    InvolvedCompany? involvedCompany = InvolvedCompanies.GetInvolvedCompanies(Id);
                    if (involvedCompany != null)
                    {
                        return Ok(involvedCompany);
                    }
                    break;

                case "MultiplayerMode":
                    MultiplayerMode? multiplayerMode = MultiplayerModes.GetGame_MultiplayerModes(Id);
                    if (multiplayerMode != null)
                    {
                        return Ok(multiplayerMode);
                    }
                    break;

                case "PlatformLogo":
                    PlatformLogo? platformLogo = PlatformLogos.GetPlatformLogo(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (platformLogo != null)
                    {
                        return Ok(platformLogo);
                    }
                    break;

                case "Platform":
                    Platform? platform = Platforms.GetPlatform(Id);
                    if (platform != null)
                    {
                        return Ok(platform);
                    }
                    break;

                case "PlatformVersion":
                    PlatformVersion? platformVersion = PlatformVersions.GetPlatformVersion(Id);
                    if (platformVersion != null)
                    {
                        return Ok(platformVersion);
                    }
                    break;

                case "PlayerPerspective":
                    PlayerPerspective? playerPerspective = PlayerPerspectives.GetGame_PlayerPerspectives(Id);
                    if (playerPerspective != null)
                    {
                        return Ok(playerPerspective);
                    }
                    break;

                case "ReleaseDate":
                    ReleaseDate? releaseDate = ReleaseDates.GetReleaseDates(Id);
                    if (releaseDate != null)
                    {
                        return Ok(releaseDate);
                    }
                    break;

                case "Screenshot":
                    Screenshot? screenshot = Screenshots.GetScreenshot(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (screenshot != null)
                    {
                        return Ok(screenshot);
                    }
                    break;

                case "Theme":
                    Theme? theme = Themes.GetGame_Themes(Id);
                    if (theme != null)
                    {
                        return Ok(theme);
                    }
                    break;

                default:
                    return NotFound();
            }

            return NotFound();
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

            List<Platform>? searchCache = Communications.GetSearchCache<List<Platform>>(searchFields, searchBody);

            if (searchCache == null)
            {
                // cache miss
                // get Platform metadata from data source
                Communications comms = new Communications(Communications.MetadataSources.IGDB);
                var results = await comms.APIComm<Platform>(IGDBClient.Endpoints.Platforms, searchFields, searchBody);

                Communications.SetSearchCache<List<Platform>>(searchFields, searchBody, results.ToList());

                return Ok(results.ToList());
            }
            else
            {
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

            List<Game>? searchCache = Communications.GetSearchCache<List<Game>>(searchFields, searchBody);

            if (searchCache == null)
            {
                // cache miss
                // get Game metadata from data source
                Communications comms = new Communications(Communications.MetadataSources.IGDB);
                var results = await comms.APIComm<Game>(IGDBClient.Endpoints.Games, searchFields, searchBody);

                List<Game> games = new List<Game>();
                foreach (Game game in results.ToList())
                {
                    Storage.CacheStatus cacheStatus = Storage.GetCacheStatus(Storage.TablePrefix.IGDB, "Game", (long)game.Id);
                    switch (cacheStatus)
                    {
                        case Storage.CacheStatus.NotPresent:
                            Storage.NewCacheValue(Storage.TablePrefix.IGDB, game, false);
                            break;

                        case Storage.CacheStatus.Expired:
                            Storage.NewCacheValue(Storage.TablePrefix.IGDB, game, true);
                            break;

                    }

                    games.Add(game);
                }

                Communications.SetSearchCache<List<Game>>(searchFields, searchBody, games);

                return Ok(games);
            }
            else
            {
                // get full version of results from database
                // this is a hacky workaround due to the readonly nature of IGDB.Model.Game IdentityOrValue fields
                List<Game> gamesToReturn = new List<Game>();
                foreach (Game game in searchCache)
                {
                    Game tempGame = Games.GetGame((long)game.Id, false, false, false);
                    gamesToReturn.Add(tempGame);
                }

                return Ok(gamesToReturn);
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

                    return PhysicalFile(imagePath, "image/jpeg");
                }
                catch (Exception ex)
                {
                    return NotFound();
                }
            }
        }
    }
}