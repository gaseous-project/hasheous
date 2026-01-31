using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Authentication
{
    public class ClientApiKey
    {
        public static string APIKeyHeaderName = "X-Client-API-Key";
        private const string APIKeyCacheNamePrefix = "ClientApiKeys";
        private const int CacheDuration = 86400; // 24 hours in seconds

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class ClientApiKeyAttribute : ServiceFilterAttribute
        {
            public ClientApiKeyAttribute()
                : base(typeof(ClientApiKeyAuthorizationFilter))
            {
            }
        }

        public class ClientApiKeyAuthorizationFilter : IAsyncAuthorizationFilter
        {
            private readonly IClientApiKeyValidator _clientApiKeyValidator;

            public ClientApiKeyAuthorizationFilter(IClientApiKeyValidator clientApiKeyValidator)
            {
                _clientApiKeyValidator = clientApiKeyValidator;
            }

            public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
            {
                string apiKey = context.HttpContext.Request.Headers[APIKeyHeaderName];

                if (apiKey == null || !await _clientApiKeyValidator.IsValidAsync(apiKey, context))
                {
                    context.Result = new UnauthorizedResult();
                }
            }
        }

        public class ClientApiKeyValidator : IClientApiKeyValidator
        {
            public async Task<bool> IsValidAsync(string apiKey, AuthorizationFilterContext context)
            {
                if (Config.RequireClientAPIKey == false)
                {
                    return true;
                }

                if (apiKey == null)
                {
                    return false;
                }

                ClientApiKeyItem? apiKeyItem = await new ClientApiKey().GetAppFromApiKeyAsync(apiKey);

                if (apiKeyItem == null || apiKeyItem.Revoked || (apiKeyItem.Expires != null && apiKeyItem.Expires < DateTime.UtcNow))
                {
                    return false;
                }

                return true;
            }
        }

        public interface IClientApiKeyValidator
        {
            Task<bool> IsValidAsync(string apiKey, AuthorizationFilterContext context);
        }

        public List<ClientApiKeyItem>? GetApiKeys(long DataObjectId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM ClientAPIKeys WHERE `DataObjectId` = @objectid ORDER BY `Created` DESC";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "objectid", DataObjectId }
            });

            List<ClientApiKeyItem> keys = new List<ClientApiKeyItem>();
            foreach (DataRow row in data.Rows)
            {
                keys.Add(new ClientApiKeyItem
                {
                    KeyId = (long)row["ClientIdIndex"],
                    Name = row["Name"].ToString(),
                    Created = (DateTime)row["Created"],
                    Expires = row["Expires"] == DBNull.Value ? null : (DateTime?)row["Expires"],
                    Revoked = (bool)row["Revoked"]
                });
            }

            return keys;
        }

        public ClientApiKeyItem? CreateApiKey(long DataObjectId, string Name, DateTime? Expires)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "INSERT INTO ClientAPIKeys (`DataObjectId`, `Name`, `APIKey`, `Created`, `Expires`, `Revoked`) VALUES (@objectid, @name, @apikey, @created, @expires, 0)";
            string apiKey = GenerateApiKey();
            db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "objectid", DataObjectId },
                { "name", Name },
                { "apikey", apiKey },
                { "created", DateTime.UtcNow },
                { "expires", Expires }
            });

            return new ClientApiKeyItem
            {
                Name = Name,
                Key = apiKey,
                Created = DateTime.Now,
                Expires = Expires,
            };
        }

        /// <summary>
        /// Retrieves an API key by its value.
        /// This method first checks the cache (Redis or in-memory) for the API key.
        /// If not found, it queries the database and caches the result for future requests.
        /// </summary>
        /// <param name="apiKey"></param>
        /// <returns>
        /// Returns a ClientApiKeyItem if the API key is found, otherwise null.
        /// The ClientApiKeyItem contains details such as KeyId, ClientAppId, Name, Key, Created, Expires, and Revoked status.
        /// If the API key is cached, it will return the cached item; otherwise, it will fetch from the database and cache it.
        /// </returns>
        public async Task<ClientApiKeyItem?> GetAppFromApiKeyAsync(string apiKey)
        {
            var keyName = APIKeyCacheNamePrefix + ":" + apiKey;

            // Check if the API key is cached - use redis if available
            // Otherwise, use a simple in-memory cache
            if (Config.RedisConfiguration.Enabled)
            {
                string? cachedValue = await hasheous.Classes.RedisConnection.GetDatabase(0).StringGetAsync(keyName);
                if (cachedValue != null)
                {
                    ClientApiKeyItem? cachedItem = Newtonsoft.Json.JsonConvert.DeserializeObject<ClientApiKeyItem>(cachedValue);

                    return cachedItem;
                }
            }
            else
            {
                // In-memory cache
                string? cachedValue = LookupCache.Get(APIKeyCacheNamePrefix, keyName);
                if (cachedValue != null)
                {
                    ClientApiKeyItem? cachedItem = Newtonsoft.Json.JsonConvert.DeserializeObject<ClientApiKeyItem>(cachedValue);

                    return cachedItem;
                }
            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM ClientAPIKeys WHERE `APIKey` = @apikey;";
            DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "apikey", apiKey }
            });

            ClientApiKeyItem? cachedApiKey = null;

            if (data.Rows.Count > 0)
            {
                DataRow row = data.Rows[0];
                cachedApiKey = new ClientApiKeyItem
                {
                    KeyId = (long)row["ClientIdIndex"],
                    ClientAppId = (long)row["DataObjectId"],
                    Name = row["Name"].ToString(),
                    Key = row["APIKey"].ToString(),
                    Created = (DateTime)row["Created"],
                    Expires = row["Expires"] == DBNull.Value ? null : (DateTime?)row["Expires"],
                    Revoked = (bool)row["Revoked"]
                };
            }

            // Cache the API key for 5 minutes
            if (cachedApiKey != null)
            {
                string serializedApiKey = Newtonsoft.Json.JsonConvert.SerializeObject(cachedApiKey);

                if (Config.RedisConfiguration.Enabled)
                {
                    await hasheous.Classes.RedisConnection.GetDatabase(0).StringSetAsync(keyName, serializedApiKey, TimeSpan.FromSeconds(CacheDuration));
                }
                else
                {
                    LookupCache.Add(APIKeyCacheNamePrefix, keyName, serializedApiKey, CacheDuration);
                }
            }

            return cachedApiKey;
        }

        public void RevokeApiKey(long DataObjectId, long ClientId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "UPDATE ClientAPIKeys SET `Revoked` = 1 WHERE `DataObjectId` = @objectid AND `ClientIdIndex` = @clientid";

            db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "objectid", DataObjectId },
                { "clientid", ClientId }
            });
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
    }
}