using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using Classes;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using Microsoft.AspNetCore.Authorization;
using hasheous_server.Models;

namespace Authentication
{
    public class ClientApiKey
    {
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class ClientApiKeyAttribute : ServiceFilterAttribute
        {
            public ClientApiKeyAttribute()
                : base(typeof(ClientApiKeyAuthorizationFilter))
            {
            }
        }

        public class ClientApiKeyAuthorizationFilter : IAuthorizationFilter
        {
            public const string ClientApiKeyHeaderName = "X-Client-API-Key";

            private readonly IClientApiKeyValidator _clientApiKeyValidator;

            public ClientApiKeyAuthorizationFilter(IClientApiKeyValidator clientApiKeyValidator)
            {
                _clientApiKeyValidator = clientApiKeyValidator;
            }

            public void OnAuthorization(AuthorizationFilterContext context)
            {
                string apiKey = context.HttpContext.Request.Headers[ClientApiKeyHeaderName];

                if (!_clientApiKeyValidator.IsValid(apiKey, ref context))
                {
                    context.Result = new UnauthorizedResult();
                }
            }
        }

        public class ClientApiKeyValidator : IClientApiKeyValidator
        {
            public bool IsValid(string apiKey, ref AuthorizationFilterContext context)
            {
                if (apiKey == null)
                {
                    return false;
                }

                if (LookupCache.Get("ClientAPIKeyCache", apiKey) == "true")
                {
                    return true;
                }

                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                string sql = "SELECT * FROM ClientAPIKeys WHERE `APIKey` = @apikey AND `Revoked` = 0 AND (Expires IS NULL OR Expires > @currenttime);";
                DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                    { "apikey", apiKey },
                    { "currenttime", DateTime.UtcNow }
                });
                if (data.Rows.Count == 0)
                {
                    LookupCache.Add("ClientAPIKeyCache", apiKey, "false");

                    return false;
                }
                else
                {
                    LookupCache.Add("ClientAPIKeyCache", apiKey, "true", 300);

                    return true;
                }
            }
        }

        public interface IClientApiKeyValidator
        {
            bool IsValid(string apiKey, ref AuthorizationFilterContext context);
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
                    ClientId = (long)row["ClientIdIndex"],
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