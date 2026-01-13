using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using Classes;
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

                // check the database
                var client = ClientManagement.GetClientByAPIKeyAndPublicId(apiKey, publicId).GetAwaiter().GetResult();
                if (client == null)
                {
                    return false;
                }

                return true;
            }
        }

        public interface ITaskWorkerAPIKeyValidator
        {
            bool IsValid(string apiKey, string publicId, ref AuthorizationFilterContext context);
        }
    }
}