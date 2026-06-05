using Authentication;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using static Authentication.ApiKey;
using static Authentication.ClientApiKey;
using static Authentication.InterHostApiKey;

public class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var securityRequirements = new List<OpenApiSecurityRequirement>();

        // get API key attribute
        var apiKeyAttribute = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<ApiKeyAttribute>();

        if (apiKeyAttribute != null && apiKeyAttribute.Count() > 0)
        {
            securityRequirements.Add(CreateSecurityRequirement("API Key"));
        }

        // get Client API key attribute
        var clientApiKeyAttribute = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<ClientApiKeyAttribute>();

        if (clientApiKeyAttribute != null && clientApiKeyAttribute.Count() > 0)
        {
            securityRequirements.Add(CreateSecurityRequirement("Client API Key"));
        }

        // get Inter host API key attribute
        var interhostApiKeyAttribute = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<InterHostApiKeyAttribute>();

        if (interhostApiKeyAttribute != null && interhostApiKeyAttribute.Count() > 0)
        {
            securityRequirements.Add(CreateSecurityRequirement("Inter Host API Key"));
        }

        if (securityRequirements.Count == 0)
        {
            operation.Security?.Clear();
            return;
        }

        operation.Security = securityRequirements;
    }

    private static OpenApiSecurityRequirement CreateSecurityRequirement(string schemeName)
    {
        return new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference(schemeName, null!, null),
                []
            }
        };
    }
}