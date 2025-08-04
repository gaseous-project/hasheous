using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using Classes;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using Microsoft.AspNetCore.Authorization;

namespace Authentication
{
    public class InterHostApiKey
    {
        public static string ApiKeyHeaderName = "X-InterHostAPI-Key";

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        public class InterHostApiKeyAttribute : ServiceFilterAttribute
        {
            public InterHostApiKeyAttribute()
                : base(typeof(InterHostApiKeyAuthorizationFilter))
            {
            }
        }

        public class InterHostApiKeyAuthorizationFilter : IAuthorizationFilter
        {
            private readonly IInterHostApiKeyValidator _apiKeyValidator;

            public InterHostApiKeyAuthorizationFilter(IInterHostApiKeyValidator apiKeyValidator)
            {
                _apiKeyValidator = apiKeyValidator;
            }

            public void OnAuthorization(AuthorizationFilterContext context)
            {
                // Check if the user is authenticated
                // Escape early if the user is already authenticated
                if (context.HttpContext.User?.Identity?.IsAuthenticated == true)
                {
                    return; // Authorized
                }

                string? apiKey = context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValue) ? headerValue.FirstOrDefault() : null;

                if (!_apiKeyValidator.IsValid(apiKey, ref context))
                {
                    context.Result = new UnauthorizedResult();
                }
            }
        }

        public class InterHostApiKeyValidator : IInterHostApiKeyValidator
        {
            public bool IsValid(string apiKey, ref AuthorizationFilterContext context)
            {
                if (apiKey == Config.ServiceCommunication.APIKey)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public interface IInterHostApiKeyValidator
        {
            bool IsValid(string apiKey, ref AuthorizationFilterContext context);
        }
    }
}