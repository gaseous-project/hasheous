using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using Classes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authentication
{
    public class ApiKey
    {
        public static string ApiKeyHeaderName = "X-API-Key";
        private const string ApiKeyCacheNamePrefix = "ApiKeys";
        private const int CacheDuration = 7200; // 2 hours in seconds

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class ApiKeyAttribute : ServiceFilterAttribute
        {
            public ApiKeyAttribute()
                : base(typeof(ApiKeyAuthorizationFilter))
            {
            }
        }

        public class ApiKeyAuthorizationFilter : IAsyncAuthorizationFilter
        {
            private readonly IApiKeyValidator _apiKeyValidator;

            public ApiKeyAuthorizationFilter(IApiKeyValidator apiKeyValidator)
            {
                _apiKeyValidator = apiKeyValidator;
            }

            public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
            {
                // Check if the user is authenticated
                // Escape early if the user is already authenticated
                if (context.HttpContext.User?.Identity?.IsAuthenticated == true)
                {
                    return; // Authorized
                }

                string? apiKey = context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValue) ? headerValue.FirstOrDefault() : null;

                if (apiKey == null || !await _apiKeyValidator.IsValidAsync(apiKey, context))
                {
                    context.Result = new UnauthorizedResult();
                }
            }
        }

        public class ApiKeyValidator : IApiKeyValidator
        {
            public async Task<bool> IsValidAsync(string apiKey, AuthorizationFilterContext context)
            {
                if (apiKey == null)
                {
                    return false;
                }

                ApplicationUser? user = await new ApiKey().GetUserFromApiKey(apiKey);
                if (user != null)
                {
                    // If the user is found, we set the ClaimsPrincipal for the context
                    var identity = new ClaimsIdentity(new List<Claim>
                    {
                        { new Claim(ClaimTypes.NameIdentifier, user.Id, ClaimValueTypes.String) },
                        { new Claim(ClaimTypes.Email, user.Email, ClaimValueTypes.String) },
                        { new Claim(ClaimTypes.Name, user.UserName, ClaimValueTypes.String) }
                    }, "Custom");

                    context.HttpContext.User = new ClaimsPrincipal(identity);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public interface IApiKeyValidator
        {
            Task<bool> IsValidAsync(string apiKey, AuthorizationFilterContext context);
        }

        public string? GetApiKey(string userId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM UserAPIKeys WHERE `UserId` = @userid";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "userid", userId }
            });

            if (data.Rows.Count > 0)
            {
                return data.Rows[0]["Key"].ToString();
            }
            else
            {
                return null;
            }
        }

        public string SetApiKey(string userId)
        {
            string newKey = GenerateApiKey();

            string? oldKey = GetApiKey(userId);

            string sql;
            if (oldKey == null)
            {
                // no existing key
                sql = "INSERT INTO UserAPIKeys (`UserId`, `Key`) VALUES (@userid, @key)";
            }
            else
            {
                sql = "UPDATE UserAPIKeys SET `Key` = @key WHERE `UserId` = @userid";
            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            db.ExecuteNonQuery(sql, new Dictionary<string, object>{
                { "userid", userId },
                { "key", newKey }
            });

            return newKey;
        }

        public async Task<ApplicationUser?> GetUserFromApiKey(string apiKey)
        {
            string cacheKey = ApiKeyCacheNamePrefix + ":" + apiKey;
            if (Config.RedisConfiguration.Enabled)
            {
                string? cachedUser = await hasheous.Classes.RedisConnection.GetDatabase(0).StringGetAsync(cacheKey);
                if (cachedUser != null)
                {
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<ApplicationUser>(cachedUser);
                }
            }
            else
            {
                // In-memory cache lookup
                string? cachedUser = LookupCache.Get(ApiKeyCacheNamePrefix, cacheKey);
                if (cachedUser != null)
                {
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<ApplicationUser>(cachedUser);
                }
            }

            // If not cached, fetch from database — check UserAPIKeys first, then UserAppKeys
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            string sql = "SELECT `UserId` FROM UserAPIKeys WHERE `Key` = @apikey";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "apikey", apiKey }
            });

            ApplicationUser? user = null;
            if (data.Rows.Count > 0)
            {
                string userId = data.Rows[0]["UserId"].ToString();
                UserStore userStore = new UserStore(db);
                user = await userStore.FindByIdAsync(userId, default);
            }
            else
            {
                // Fall through to app-linked keys
                string appKeySql = "SELECT `UserId` FROM UserAppKeys WHERE `APIKey` = @apikey AND `Revoked` = 0";
                DataTable appKeyData = db.ExecuteCMD(appKeySql, new Dictionary<string, object>{
                    { "apikey", apiKey }
                });

                if (appKeyData.Rows.Count > 0)
                {
                    string userId = appKeyData.Rows[0]["UserId"].ToString();
                    UserStore userStore = new UserStore(db);
                    user = await userStore.FindByIdAsync(userId, default);

                    if (user != null)
                    {
                        // Update LastUsed timestamp asynchronously (fire-and-forget, non-critical)
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                Database updateDb = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                                updateDb.ExecuteNonQuery(
                                    "UPDATE UserAppKeys SET `LastUsed` = @lastuused WHERE `APIKey` = @apikey",
                                    new Dictionary<string, object> { { "lastuused", DateTime.UtcNow }, { "apikey", apiKey } }
                                );
                            }
                            catch { /* best-effort */ }
                        });
                    }
                }
            }

            // Cache the user
            string serializedUser = Newtonsoft.Json.JsonConvert.SerializeObject(user);
            if (Config.RedisConfiguration.Enabled)
            {
                await hasheous.Classes.RedisConnection.GetDatabase(0).StringSetAsync(cacheKey, serializedUser, TimeSpan.FromSeconds(CacheDuration));
            }
            else
            {
                LookupCache.Add(ApiKeyCacheNamePrefix, cacheKey, serializedUser, CacheDuration);
            }

            return user;
        }

        private string GenerateApiKey()
        {
            int keyLength = 64;

            byte[] bytes = RandomNumberGenerator.GetBytes(keyLength);

            string base64String = Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_");

            return base64String[..keyLength];
        }

        /// <summary>
        /// Purges the cached API key entry for a given raw key string.
        /// Call this after revoking an app-linked key so the cache does not serve the revoked key.
        /// In-memory cache entries expire within the configured cache duration (2 hours).
        /// </summary>
        public static void PurgeApiKeyCache(string apiKey)
        {
            if (Config.RedisConfiguration.Enabled)
            {
                string cacheKey = ApiKeyCacheNamePrefix + ":" + apiKey;
                hasheous.Classes.RedisConnection.GetDatabase(0).KeyDelete(cacheKey);
            }
            // In-memory cache entries will expire naturally after CacheDuration seconds
        }
    }
}