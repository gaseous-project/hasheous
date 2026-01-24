using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using Classes;
using hasheous.Classes;
using hasheous_server.Classes.Tasks.Clients;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StackExchange.Redis;

namespace Authentication
{
    public class TaskWorkerAPIKey
    {
        public static string APIKeyHeaderName = "X-TaskWorker-API-Key";
        private const string APIKeyCacheNamePrefix = "TaskWorkerAPIKeys";
        private const int CacheDuration = 86400; // 24 hours in seconds

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class TaskWorkerAPIKeyAttribute : ServiceFilterAttribute
        {
            public TaskWorkerAPIKeyAttribute()
                : base(typeof(TaskWorkerAPIKeyAuthorizationFilter))
            {
            }
        }

        public class TaskWorkerAPIKeyAuthorizationFilter : IAuthorizationFilter
        {
            private readonly ITaskWorkerAPIKeyValidator _TaskWorkerAPIKeyValidator;

            public TaskWorkerAPIKeyAuthorizationFilter(ITaskWorkerAPIKeyValidator TaskWorkerAPIKeyValidator)
            {
                _TaskWorkerAPIKeyValidator = TaskWorkerAPIKeyValidator;
            }

            public void OnAuthorization(AuthorizationFilterContext context)
            {
                string apiKey = context.HttpContext.Request.Headers[APIKeyHeaderName];
                string publicId = (string)context.RouteData.Values["publicid"];

                if (!_TaskWorkerAPIKeyValidator.IsValid(apiKey, publicId, ref context))
                {
                    context.Result = new UnauthorizedResult();
                }
            }
        }

        public class TaskWorkerAPIKeyValidator : ITaskWorkerAPIKeyValidator
        {
            public bool IsValid(string apiKey, string publicId, ref AuthorizationFilterContext context)
            {
                if (apiKey == null || publicId == null)
                {
                    return false;
                }

                var cacheKey = RedisConnection.GenerateKey(APIKeyCacheNamePrefix, publicId + apiKey);
                // check the cache first
                if (Config.RedisConfiguration.Enabled)
                {
                    string? cachedValue = hasheous.Classes.RedisConnection.GetDatabase(0).StringGet(cacheKey);
                    if (cachedValue != null)
                    {
                        bool cachedItem = Newtonsoft.Json.JsonConvert.DeserializeObject<bool>(cachedValue);

                        return cachedItem;
                    }
                }

                // check the database
                DataTable dt = Config.database.ExecuteCMD("SELECT * FROM Task_Clients WHERE api_key = @api_key AND public_id = @public_id LIMIT 1;", new Dictionary<string, object>
                {
                    { "@api_key", apiKey },
                    { "@public_id", publicId }
                });
                bool isValid;
                if (dt.Rows.Count == 0)
                {
                    isValid = false;
                }
                else
                {
                    isValid = true;
                }

                // store in cache
                if (Config.RedisConfiguration.Enabled)
                {
                    hasheous.Classes.RedisConnection.GetDatabase(0).StringSet(cacheKey, Newtonsoft.Json.JsonConvert.SerializeObject(isValid), TimeSpan.FromSeconds(CacheDuration));
                }

                return isValid;
            }
        }

        public interface ITaskWorkerAPIKeyValidator
        {
            bool IsValid(string apiKey, string publicId, ref AuthorizationFilterContext context);
        }
    }
}