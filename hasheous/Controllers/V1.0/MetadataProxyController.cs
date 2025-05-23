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
        /// Get Artwork metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Artwork object to fetch
        /// </param>
        /// <param name="slug">
        /// The slug of the Artwork object to fetch
        /// </param>
        /// <returns>
        /// The Artwork metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.AgeRating), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/AgeRating")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_AgeRating(long Id)
        {
            return await _GetMetadata("AgeRating", Id);
        }

        /// <summary>
        /// Get AgeRatingContentDescription metadata from IGDB
        /// </summary>
        /// <param name="Id">The Id of the AgeRatingContentDescription object to fetch</param>
        /// <returns>
        /// The AgeRatingContentDescription metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.AgeRatingContentDescription), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/AgeRatingContentDescription")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_AgeRatingContentDescription(long Id)
        {
            return await _GetMetadata("AgeRatingContentDescription", Id);
        }

        /// <summary>
        /// Get AlternativeName metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the AlternativeName object to fetch
        /// </param>
        /// <returns>
        /// The AlternativeName metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.AlternativeName), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/AlternativeName")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_AlternativeName(long Id)
        {
            return await _GetMetadata("AlternativeName", Id);
        }

        /// <summary>
        /// Get Artwork metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Artwork object to fetch
        /// </param>
        /// <returns>
        /// The Artwork metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Artwork), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Artwork")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Artwork(long Id)
        {
            return await _GetMetadata("Artwork", Id);
        }

        /// <summary>
        /// Get Collection metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Collection object to fetch 
        /// </param>
        /// <param name="slug">
        /// The slug of the Collection object to fetch 
        /// </param>
        /// <returns>
        /// The Collection metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Collection), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Collection")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Collection(long Id, string slug = "")
        {
            return await _GetMetadata("Collection", Id, slug);
        }

        /// <summary>
        /// Get Company metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Company object to fetch
        /// </param>
        /// <param name="slug">
        /// The slug of the Company object to fetch
        /// </param>
        /// <returns>
        /// The Company metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Company), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Company")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Company(long Id, string slug = "")
        {
            return await _GetMetadata("Company", Id, slug);
        }

        /// <summary>
        /// Get CompanyLogo metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the CompanyLogo object to fetch
        /// </param>
        /// <returns>
        /// The CompanyLogo metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.CompanyLogo), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/CompanyLogo")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_CompanyLogo(long Id)
        {
            return await _GetMetadata("CompanyLogo", Id);
        }

        /// <summary>
        /// Get Cover metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Cover object to fetch
        /// </param>
        /// <returns>
        /// The Cover metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Cover), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Cover")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Cover(long Id)
        {
            return await _GetMetadata("Cover", Id);
        }

        /// <summary>
        /// Get ExternalGame metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the ExternalGame object to fetch
        /// </param>
        /// <returns>
        /// The ExternalGame metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.ExternalGame), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/ExternalGame")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_ExternalGame(long Id)
        {
            return await _GetMetadata("ExternalGame", Id);
        }

        /// <summary>
        /// Get Franchise metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Franchise object to fetch
        /// </param>
        /// <param name="slug">
        /// The slug of the Franchise object to fetch
        /// </param>
        /// <returns>
        /// The Franchise metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Franchise), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Franchise")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Franchise(long Id, string slug = "")
        {
            return await _GetMetadata("Franchise", Id, slug);
        }

        /// <summary>
        /// Get GameMode metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the GameMode object to fetch
        /// </param>
        /// <returns>
        /// The GameMode metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.GameMode), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/GameMode")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_GameMode(long Id)
        {
            return await _GetMetadata("GameMode", Id);
        }

        /// <summary>
        /// Get GameLocalisation metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the GameLocalisation object to fetch
        /// </param>
        /// <returns>
        /// The GameLocalisation metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.GameLocalization), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/GameLocalization")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_GameLocalisations(long Id)
        {
            return await _GetMetadata("GameLocalization", Id);
        }

        /// <summary>
        /// Get Game metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Game object to fetch
        /// </param>
        /// <param name="slug">
        /// The slug of the Game object to fetch
        /// </param>
        /// <returns>
        /// The Game metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Game), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Game")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Game(long Id, string slug = "")
        {
            return await _GetMetadata("Game", Id, slug);
        }

        /// <summary>
        /// Get GameVideo metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the GameVideo object to fetch
        /// </param>
        /// <returns>
        /// The GameVideo metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.GameVideo), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/GameVideo")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_GameVideo(long Id)
        {
            return await _GetMetadata("GameVideo", Id);
        }

        /// <summary>
        /// Get Genre metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Genre object to fetch
        /// </param>
        /// <returns>
        /// The Genre metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Genre), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Genre")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Genre(long Id)
        {
            return await _GetMetadata("Genre", Id);
        }

        /// <summary>
        /// Get InvolvedCompany metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the InvolvedCompany object to fetch
        /// </param>
        /// <returns>
        /// The InvolvedCompany metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.InvolvedCompany), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/InvolvedCompany")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_InvolvedCompany(long Id)
        {
            return await _GetMetadata("InvolvedCompany", Id);
        }

        /// <summary>
        /// Get MultiplayerMode metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the MultiplayerMode object to fetch
        /// </param>
        /// <returns>
        /// The MultiplayerMode metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.MultiplayerMode), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/MultiplayerMode")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_MultiplayerMode(long Id)
        {
            return await _GetMetadata("MultiplayerMode", Id);
        }

        /// <summary>
        /// Get PlatformLogo metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the PlatformLogo object to fetch
        /// </param>
        /// <returns>
        /// The PlatformLogo metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.PlatformLogo), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/PlatformLogo")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_PlatformLogo(long Id)
        {
            return await _GetMetadata("PlatformLogo", Id);
        }

        /// <summary>
        /// Get Platform metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Platform object to fetch
        /// </param>
        /// <param name="slug">
        /// The slug of the Platform object to fetch
        /// </param>
        /// <returns>
        /// The Platform metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Platform), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Platform")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Platform(long Id, string slug = "")
        {
            return await _GetMetadata("Platform", Id, slug);
        }

        /// <summary>
        /// Get PlatformVersion metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the PlatformVersion object to fetch
        /// </param>
        /// <returns>
        /// The PlatformVersion metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.PlatformVersion), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/PlatformVersion")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_PlatformVersion(long Id)
        {
            return await _GetMetadata("PlatformVersion", Id);
        }

        /// <summary>
        /// Get PlayerPerspective metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the PlayerPerspective object to fetch
        /// </param>
        /// <returns>
        /// The PlayerPerspective metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.PlayerPerspective), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/PlayerPerspective")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_PlayerPerspective(long Id)
        {
            return await _GetMetadata("PlayerPerspective", Id);
        }

        /// <summary>
        /// Get ReleaseDate metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the ReleaseDate object to fetch
        /// </param>
        /// <returns>
        /// The ReleaseDate metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.ReleaseDate), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/ReleaseDate")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_ReleaseDate(long Id)
        {
            return await _GetMetadata("ReleaseDate", Id);
        }

        /// <summary>
        /// Get Region metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Region object to fetch
        /// </param>
        /// <returns>
        /// The Region metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Region), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Region")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Region(long Id)
        {
            return await _GetMetadata("Region", Id);
        }

        /// <summary>
        /// Get Screenshot metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Screenshot object to fetch
        /// </param>
        /// <returns>
        /// The Screenshot metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Screenshot), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Screenshot")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Screenshot(long Id)
        {
            return await _GetMetadata("Screenshot", Id);
        }

        /// <summary>
        /// Get Theme metadata from IGDB
        /// </summary>
        /// <param name="Id">
        /// The Id of the Theme object to fetch
        /// </param>
        /// <returns>
        /// The Theme metadata object from IGDB
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
        [ProducesResponseType(typeof(HasheousClient.Models.Metadata.IGDB.Theme), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Route("IGDB/Theme")]
        [ResponseCache(CacheProfileName = "7Days")]
        public async Task<IActionResult> GetMetadata_Theme(long Id)
        {
            return await _GetMetadata("Theme", Id);
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

            // get metadata
            switch (routeName)
            {
                case "AgeRating":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    AgeRating? ageRating = await AgeRatings.GetAgeRatings(Id);
                    if (ageRating != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.AgeRating>(ageRating, new HasheousClient.Models.Metadata.IGDB.AgeRating());
                    }
                    break;

                case "AgeRatingContentDescription":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    AgeRatingContentDescription? ageRatingContentDescription = await AgeRatingContentDescriptions.GetAgeRatingContentDescriptions(Id);
                    if (ageRatingContentDescription != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.AgeRatingContentDescription>(ageRatingContentDescription, new HasheousClient.Models.Metadata.IGDB.AgeRatingContentDescription());
                    }
                    break;

                case "AlternativeName":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    AlternativeName? alternativeName = await AlternativeNames.GetAlternativeNames(Id);
                    if (alternativeName != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.AlternativeName>(alternativeName, new HasheousClient.Models.Metadata.IGDB.AlternativeName());
                    }
                    break;

                case "Artwork":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    Artwork? artwork = await Artworks.GetArtwork(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (artwork != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Artwork>(artwork, new HasheousClient.Models.Metadata.IGDB.Artwork());
                    }
                    break;

                case "Collection":
                    supportsSlugSearch = true;
                    Collection? collection = null;

                    if (isSlugSearch == true)
                    {
                        collection = await Collections.GetCollections(slug);
                    }
                    else
                    {
                        collection = await Collections.GetCollections(Id);
                    }

                    if (collection != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Collection>(collection, new HasheousClient.Models.Metadata.IGDB.Collection());
                    }
                    break;

                case "Company":
                    supportsSlugSearch = true;
                    Company? company = null;

                    if (isSlugSearch == true)
                    {
                        company = await Companies.GetCompanies(slug);
                    }
                    else
                    {
                        company = await Companies.GetCompanies(Id);
                    }

                    if (company != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Company>(company, new HasheousClient.Models.Metadata.IGDB.Company());
                    }
                    break;

                case "CompanyLogo":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    CompanyLogo? companyLogo = await CompanyLogos.GetCompanyLogo(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (companyLogo != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.CompanyLogo>(companyLogo, new HasheousClient.Models.Metadata.IGDB.CompanyLogo());
                    }
                    break;

                case "Cover":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    Cover? cover = await Covers.GetCover(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (cover != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Cover>(cover, new HasheousClient.Models.Metadata.IGDB.Cover());
                    }
                    break;

                case "ExternalGame":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    ExternalGame? externalGame = await ExternalGames.GetExternalGames(Id);
                    if (externalGame != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.ExternalGame>(externalGame, new HasheousClient.Models.Metadata.IGDB.ExternalGame());
                    }
                    break;

                case "Franchise":
                    supportsSlugSearch = true;
                    Franchise? franchise = null;

                    if (isSlugSearch == true)
                    {
                        franchise = await Franchises.GetFranchises(slug);
                    }
                    else
                    {
                        franchise = await Franchises.GetFranchises(Id);
                    }

                    if (franchise != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Franchise>(franchise, new HasheousClient.Models.Metadata.IGDB.Franchise());
                    }
                    break;

                case "GameMode":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    GameMode? gameMode = await GameModes.GetGame_Modes(Id);
                    if (gameMode != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.GameMode>(gameMode, new HasheousClient.Models.Metadata.IGDB.GameMode());
                    }
                    break;

                case "Game":
                    supportsSlugSearch = true;
                    Game? game = null;

                    if (isSlugSearch == true)
                    {
                        game = await Games.GetGame(slug, false, false, false);
                    }
                    else
                    {
                        game = await Games.GetGame(Id, false, false, false);
                    }

                    if (game != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Game>(game, new HasheousClient.Models.Metadata.IGDB.Game());
                    }
                    break;

                case "GameLocalization":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    GameLocalization? gameLocalisation = await GameLocalisations.GetGame_Localisations(Id);
                    if (gameLocalisation != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.GameLocalization>(gameLocalisation, new HasheousClient.Models.Metadata.IGDB.GameLocalization());
                    }
                    break;

                case "GameVideo":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    GameVideo? gameVideo = await GamesVideos.GetGame_Videos(Id);
                    if (gameVideo != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.GameVideo>(gameVideo, new HasheousClient.Models.Metadata.IGDB.GameVideo());
                    }
                    break;

                case "Genre":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    Genre? genre = await Genres.GetGenres(Id);
                    if (genre != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Genre>(genre, new HasheousClient.Models.Metadata.IGDB.Genre());
                    }
                    break;

                case "InvolvedCompany":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    InvolvedCompany? involvedCompany = await InvolvedCompanies.GetInvolvedCompanies(Id);
                    if (involvedCompany != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.InvolvedCompany>(involvedCompany, new HasheousClient.Models.Metadata.IGDB.InvolvedCompany());
                    }
                    break;

                case "MultiplayerMode":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    MultiplayerMode? multiplayerMode = await MultiplayerModes.GetGame_MultiplayerModes(Id);
                    if (multiplayerMode != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.MultiplayerMode>(multiplayerMode, new HasheousClient.Models.Metadata.IGDB.MultiplayerMode());
                    }
                    break;

                case "PlatformLogo":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    PlatformLogo? platformLogo = await PlatformLogos.GetPlatformLogo(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (platformLogo != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.PlatformLogo>(platformLogo, new HasheousClient.Models.Metadata.IGDB.PlatformLogo());
                    }
                    break;

                case "Platform":
                    supportsSlugSearch = true;
                    Platform? platform = null;

                    if (isSlugSearch == true)
                    {
                        platform = await Platforms.GetPlatform(slug);
                    }
                    else
                    {
                        platform = await Platforms.GetPlatform(Id);
                    }

                    if (platform != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Platform>(platform, new HasheousClient.Models.Metadata.IGDB.Platform());
                    }
                    break;

                case "PlatformVersion":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    PlatformVersion? platformVersion = await PlatformVersions.GetPlatformVersion(Id);
                    if (platformVersion != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.PlatformVersion>(platformVersion, new HasheousClient.Models.Metadata.IGDB.PlatformVersion());
                    }
                    break;

                case "PlayerPerspective":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    PlayerPerspective? playerPerspective = await PlayerPerspectives.GetGame_PlayerPerspectives(Id);
                    if (playerPerspective != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.PlayerPerspective>(playerPerspective, new HasheousClient.Models.Metadata.IGDB.PlayerPerspective());
                    }
                    break;

                case "ReleaseDate":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    ReleaseDate? releaseDate =await ReleaseDates.GetReleaseDates(Id);
                    if (releaseDate != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.ReleaseDate>(releaseDate, new HasheousClient.Models.Metadata.IGDB.ReleaseDate());
                    }
                    break;

                case "Region":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    IGDB.Models.Region? region =await  Regions.GetGame_Regions(Id);
                    if (region != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Region>(region, new HasheousClient.Models.Metadata.IGDB.Region());
                    }
                    break;

                case "Screenshot":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    Screenshot? screenshot =await  Screenshots.GetScreenshot(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
                    if (screenshot != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Screenshot>(screenshot, new HasheousClient.Models.Metadata.IGDB.Screenshot());
                    }
                    break;

                case "Theme":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    Theme? theme = await Themes.GetGame_Themes(Id);
                    if (theme != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Theme>(theme, new HasheousClient.Models.Metadata.IGDB.Theme());
                    }
                    break;

                default:
                    return NotFound();
            }

            if (isSlugSearch == true && supportsSlugSearch == false)
            {
                return BadRequest();
            }

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