using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using Classes;
using Classes.Metadata;
using hasheous_server.Classes.Metadata;
using hasheous_server.Classes.Metadata.IGDB;
using HasheousClient;
using IGDB;
using IGDB.Models;
using Microsoft.AspNetCore.Mvc;
using static Authentication.ApiKey;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Endpoints used for proxying metadata from IGDB
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [ApiKey()]
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

                    AgeRating? ageRating = AgeRatings.GetAgeRatings(Id);
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

                    AgeRatingContentDescription? ageRatingContentDescription = AgeRatingContentDescriptions.GetAgeRatingContentDescriptions(Id);
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

                    AlternativeName? alternativeName = AlternativeNames.GetAlternativeNames(Id);
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

                    Artwork? artwork = Artworks.GetArtwork(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
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
                        collection = Collections.GetCollections(slug);
                    }
                    else
                    {
                        collection = Collections.GetCollections(Id);
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
                        company = Companies.GetCompanies(slug);
                    }
                    else
                    {
                        company = Companies.GetCompanies(Id);
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

                    CompanyLogo? companyLogo = CompanyLogos.GetCompanyLogo(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
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

                    Cover? cover = Covers.GetCover(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
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

                    ExternalGame? externalGame = ExternalGames.GetExternalGames(Id);
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
                        franchise = Franchises.GetFranchises(slug);
                    }
                    else
                    {
                        franchise = Franchises.GetFranchises(Id);
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

                    GameMode? gameMode = GameModes.GetGame_Modes(Id);
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
                        game = Games.GetGame(slug, false, false, false);
                    }
                    else
                    {
                        game = Games.GetGame(Id, false, false, false);
                    }

                    if (game != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.Game>(game, new HasheousClient.Models.Metadata.IGDB.Game());
                    }
                    break;

                case "GameVideo":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    GameVideo? gameVideo = GamesVideos.GetGame_Videos(Id);
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

                    Genre? genre = Genres.GetGenres(Id);
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

                    InvolvedCompany? involvedCompany = InvolvedCompanies.GetInvolvedCompanies(Id);
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

                    MultiplayerMode? multiplayerMode = MultiplayerModes.GetGame_MultiplayerModes(Id);
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

                    PlatformLogo? platformLogo = PlatformLogos.GetPlatformLogo(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
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
                        platform = Platforms.GetPlatform(slug);
                    }
                    else
                    {
                        platform = Platforms.GetPlatform(Id);
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

                    PlatformVersion? platformVersion = PlatformVersions.GetPlatformVersion(Id);
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

                    PlayerPerspective? playerPerspective = PlayerPerspectives.GetGame_PlayerPerspectives(Id);
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

                    ReleaseDate? releaseDate = ReleaseDates.GetReleaseDates(Id);
                    if (releaseDate != null)
                    {
                        returnValue = HasheousClient.Models.Metadata.IGDB.ITools.ConvertFromIGDB<HasheousClient.Models.Metadata.IGDB.ReleaseDate>(releaseDate, new HasheousClient.Models.Metadata.IGDB.ReleaseDate());
                    }
                    break;

                case "Screenshot":
                    supportsSlugSearch = false;
                    if (isSlugSearch)
                    {
                        break;
                    }

                    Screenshot? screenshot = Screenshots.GetScreenshot(Id, Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB);
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

                    Theme? theme = Themes.GetGame_Themes(Id);
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