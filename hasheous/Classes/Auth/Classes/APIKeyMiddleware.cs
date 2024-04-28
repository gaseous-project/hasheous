using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using Classes;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;

namespace Authentication
{
    public class ApiKey
    {
        public class ApiKeyAttribute : ServiceFilterAttribute
        {
            public ApiKeyAttribute()
                : base(typeof(ApiKeyAuthorizationFilter))
            {
            }
        }

        public class ApiKeyAuthorizationFilter : IAuthorizationFilter
        {
            private const string ApiKeyHeaderName = "X-API-Key";

            private readonly IApiKeyValidator _apiKeyValidator;

            public ApiKeyAuthorizationFilter(IApiKeyValidator apiKeyValidator)
            {
                _apiKeyValidator = apiKeyValidator;
            }

            public void OnAuthorization(AuthorizationFilterContext context)
            {
                string apiKey = context.HttpContext.Request.Headers[ApiKeyHeaderName];

                if (!_apiKeyValidator.IsValid(apiKey, ref context))
                {
                    context.Result = new UnauthorizedResult();
                }
            }
        }

        public class ApiKeyValidator : IApiKeyValidator
        {
            public bool IsValid(string apiKey, ref AuthorizationFilterContext context)
            {
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                string sql = "SELECT * FROM UserAPIKeys WHERE `Key` = @apikey";
                DataTable data = db.ExecuteCMD(sql, new Dictionary<string, object>{
                    { "apikey", apiKey }
                });
                if (data.Rows.Count == 0)
                {
                    return false;
                }
                else
                {
                    UserStore userStore = new UserStore(db);
                    var userAccount = userStore.FindByIdAsync(data.Rows[0]["UserID"].ToString(), default);
                    if (userAccount.Result != null)
                    {
                        var identity = new ClaimsIdentity(new List<Claim>
                        {
                            { new Claim(ClaimTypes.NameIdentifier, userAccount.Result.Id, ClaimValueTypes.String) },
                            { new Claim(ClaimTypes.Email, userAccount.Result.Email, ClaimValueTypes.String) },
                            { new Claim(ClaimTypes.Name, userAccount.Result.UserName, ClaimValueTypes.String) }
                        }, "Custom");

                        context.HttpContext.User = new ClaimsPrincipal(identity);
                    }

                    return true;
                }
            }
        }

        public interface IApiKeyValidator
        {
            bool IsValid(string apiKey, ref AuthorizationFilterContext context);
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