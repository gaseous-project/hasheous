using System.Data;
using System.Security.Cryptography;
using Authentication;
using Classes;
using hasheous_server.Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static hasheous_server.Classes.DataObjects;

namespace hasheous_server.Controllers.v1_0
{
    /// <summary>
    /// Provides an OAuth-style flow that allows client applications to request a per-user
    /// API key by presenting their <c>X-Client-API-Key</c>. The user confirms the link in a
    /// popup window and the resulting key is then accepted by all endpoints protected with
    /// the <c>[ApiKey()]</c> attribute.
    /// </summary>
    /// <remarks>
    /// <para><b>Integration guide for client applications</b></para>
    /// <para>1. Open the authorisation popup:</para>
    /// <code>
    /// const popup = window.open(
    ///   '/pages/link-app.html?clientApiKey=YOUR_CLIENT_API_KEY',
    ///   'hasheousLink',
    ///   'width=480,height=640'
    /// );
    /// </code>
    /// <para>2. Listen for the result:</para>
    /// <code>
    /// window.addEventListener('message', (event) => {
    ///   if (event.data?.type === 'hasheous-link') {
    ///     if (event.data.cancelled) {
    ///       // User cancelled
    ///     } else {
    ///       const apiKey = event.data.hasheousApiKey;
    ///       // Store apiKey and pass it as the X-API-Key header on future requests
    ///     }
    ///   }
    /// });
    /// </code>
    /// <para>3. Error cases to handle:</para>
    /// <list type="bullet">
    ///   <item>The popup shows a polite refusal message when the client key cannot be resolved to an App DataObject.</item>
    ///   <item><c>event.data.cancelled === true</c> when the user closes the popup without confirming.</item>
    /// </list>
    /// </remarks>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AppLinkController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AppLinkController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// Returns display information for the application identified by the supplied client API key.
        /// </summary>
        /// <remarks>
        /// This endpoint is intentionally anonymous so the popup page can call it before the user
        /// has logged in and display the app's name and logo to them.
        /// </remarks>
        /// <param name="clientApiKey">The value of the client application's API key.</param>
        /// <returns>
        /// <see cref="AppInfoViewModel"/> on success, or a 404 with a message when the key cannot
        /// be resolved to a valid, non-revoked App-type DataObject.
        /// </returns>
        [HttpGet]
        [AllowAnonymous]
        [Route("AppInfo")]
        [ProducesResponseType(typeof(AppInfoViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AppInfo([FromQuery] string clientApiKey)
        {
            var info = await ResolveAppFromClientKey(clientApiKey);
            if (info == null)
            {
                return NotFound(new { message = "The application could not be identified. Please contact the application developer." });
            }

            return Ok(info);
        }

        /// <summary>
        /// Authorises the identified application to act as the currently signed-in user and returns
        /// a per-user API key scoped to that application.
        /// </summary>
        /// <remarks>
        /// <para>If the user has already authorised this application, the existing key is returned
        /// (idempotent). If the key was previously revoked, it is re-issued.</para>
        /// <para>The returned key must be supplied as the <c>X-API-Key</c> header on subsequent
        /// requests to Hasheous endpoints that require user authentication.</para>
        /// </remarks>
        /// <param name="model">Request body containing the client application's API key.</param>
        /// <returns>The raw API key string on success.</returns>
        [HttpPost]
        [Authorize]
        [Route("Authorize")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Authorize([FromBody] AuthorizeRequestModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var info = await ResolveAppFromClientKey(model.ClientApiKey);
            if (info == null)
            {
                return NotFound(new { message = "The application could not be identified. Please contact the application developer." });
            }

            string apiKey = UpsertUserAppKey(user.Id, info.DataObjectId);
            return Ok(apiKey);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Looks up a ClientAPIKey, verifies it belongs to an App-type DataObject, and
        /// returns display info.  Returns null on any validation failure.
        /// </summary>
        private async Task<AppInfoViewModel?> ResolveAppFromClientKey(string? clientApiKey)
        {
            if (string.IsNullOrWhiteSpace(clientApiKey))
            {
                return null;
            }

            ClientApiKey clientKeyHelper = new ClientApiKey();
            ClientApiKeyItem? keyItem = await clientKeyHelper.GetAppFromApiKeyAsync(clientApiKey);

            if (keyItem == null || keyItem.Revoked || (keyItem.Expires != null && keyItem.Expires < DateTime.UtcNow))
            {
                return null;
            }

            // The DataObject linked to the client key must be of type App
            DataObjects dataObjects = new DataObjects();
            DataObjectItem? appObject = await dataObjects.GetDataObject(DataObjectType.App, keyItem.ClientAppId!.Value, false, false, false);

            if (appObject == null)
            {
                return null;
            }

            // Extract logo image URL (Logo attribute)
            string? logoUrl = null;
            var logoAttr = appObject.Attributes?.FirstOrDefault(a => a.attributeName == AttributeItem.AttributeName.Logo);
            if (logoAttr?.Value != null)
            {
                string logoValue = logoAttr.Value.ToString()!;
                logoUrl = $"/api/v1/Images/{logoValue}";
            }

            return new AppInfoViewModel
            {
                DataObjectId = appObject.Id,
                Name = appObject.Name,
                LogoUrl = logoUrl
            };
        }

        /// <summary>
        /// Upserts a row in UserAppKeys for the given user/app pair and returns the API key.
        /// </summary>
        private string UpsertUserAppKey(string userId, long dataObjectId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            // Check for an existing row (active or revoked)
            string selectSql = "SELECT `Id`, `APIKey`, `Revoked` FROM UserAppKeys WHERE `UserId` = @userid AND `DataObjectId` = @dataobjectid";
            DataTable existing = db.ExecuteCMD(selectSql, new Dictionary<string, object>
            {
                { "userid", userId },
                { "dataobjectid", dataObjectId }
            });

            if (existing.Rows.Count > 0)
            {
                bool revoked = (bool)existing.Rows[0]["Revoked"];
                string currentKey = existing.Rows[0]["APIKey"].ToString()!;
                long rowId = (long)existing.Rows[0]["Id"];

                if (!revoked)
                {
                    // Already active – return the existing key (idempotent)
                    return currentKey;
                }

                // Was revoked – issue a new key and un-revoke
                string newKey = GenerateApiKey();
                db.ExecuteNonQuery(
                    "UPDATE UserAppKeys SET `APIKey` = @apikey, `Revoked` = 0, `LastUsed` = NULL WHERE `Id` = @id",
                    new Dictionary<string, object> { { "apikey", newKey }, { "id", rowId } }
                );
                // Purge the old revoked key from cache just in case
                ApiKey.PurgeApiKeyCache(currentKey);
                return newKey;
            }
            else
            {
                // No existing row – insert a new one
                string newKey = GenerateApiKey();
                db.ExecuteNonQuery(
                    "INSERT INTO UserAppKeys (`UserId`, `DataObjectId`, `APIKey`) VALUES (@userid, @dataobjectid, @apikey)",
                    new Dictionary<string, object>
                    {
                        { "userid", userId },
                        { "dataobjectid", dataObjectId },
                        { "apikey", newKey }
                    }
                );
                return newKey;
            }
        }

        private static string GenerateApiKey()
        {
            int keyLength = 64;
            byte[] bytes = RandomNumberGenerator.GetBytes(keyLength);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                [..keyLength];
        }

        // ─── View models ─────────────────────────────────────────────────────────

        /// <summary>Display information about an application DataObject.</summary>
        public class AppInfoViewModel
        {
            /// <summary>The DataObject ID of the application.</summary>
            public long DataObjectId { get; set; }
            /// <summary>The display name of the application.</summary>
            public string Name { get; set; } = string.Empty;
            /// <summary>URL of the application logo image, or null if none is set.</summary>
            public string? LogoUrl { get; set; }
        }

        /// <summary>Request body for the Authorize endpoint.</summary>
        public class AuthorizeRequestModel
        {
            /// <summary>The client application's API key (value of the <c>X-Client-API-Key</c> header).</summary>
            public string ClientApiKey { get; set; } = string.Empty;
        }
    }
}
