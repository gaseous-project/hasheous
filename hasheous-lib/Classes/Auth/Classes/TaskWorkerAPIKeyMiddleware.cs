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

                if (!_TaskWorkerAPIKeyValidator.IsValid(apiKey, ref context))
                {
                    context.Result = new UnauthorizedResult();
                }
            }
        }

        public class TaskWorkerAPIKeyValidator : ITaskWorkerAPIKeyValidator
        {
            public bool IsValid(string apiKey, ref AuthorizationFilterContext context)
            {
                if (apiKey == null)
                {
                    return false;
                }

                // Check if the API key exists and is valid

                return true;
            }
        }

        public interface ITaskWorkerAPIKeyValidator
        {
            bool IsValid(string apiKey, ref AuthorizationFilterContext context);
        }
    }
}