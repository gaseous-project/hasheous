using Authentication;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using static Authentication.ApiKey;
using static Authentication.ClientApiKey;

public class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        List<string> securityRequirements = new List<string>();

        // get API key attribute
        var apiKeyAttribute = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<ApiKeyAttribute>();

        if (apiKeyAttribute != null && apiKeyAttribute.Count() > 0)
        {
            securityRequirements.Add("API Key");


            // add security requirement
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "API Key"
                            }
                        },
                        securityRequirements
                    }
                }
            };
        }

        // get Client API key attribute
        var clientApiKeyAttribute = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<ClientApiKeyAttribute>();

        if (clientApiKeyAttribute != null && clientApiKeyAttribute.Count() > 0)
        {
            securityRequirements.Add("Client API Key");

            // add security requirement
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Client API Key"
                            }
                        },
                        securityRequirements
                    }
                }
            };
        }

        if (securityRequirements.Count == 0)
        {
            operation.Security.Clear();
        }
    }
}