using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using static Authentication.ApiKey;

public class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // get API key attribute
        var apiKeyAttribute = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<ApiKeyAttribute>();

        if (apiKeyAttribute != null && apiKeyAttribute.Count() > 0)
        {
            List<string> securityRequirements = new List<string>();
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
        else
        {
            operation.Security.Clear();
        }
    }
}