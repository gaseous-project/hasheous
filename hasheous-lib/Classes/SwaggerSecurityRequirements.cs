using Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using static Authentication.ApiKey;
using static Authentication.ClientApiKey;
using static Authentication.InterHostApiKey;

public class AuthorizationOperationFilter : IOperationFilter
{
    private static readonly OpenApiDocument SecurityReferenceDocument = CreateSecurityReferenceDocument();

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var securityRequirements = new List<OpenApiSecurityRequirement>();

        if (HasApiKeyAttribute<ApiKeyAttribute>(context) || HasServiceFilter<ApiKeyAuthorizationFilter>(context))
        {
            securityRequirements.Add(CreateSecurityRequirement("API Key"));
        }

        if ((HasApiKeyAttribute<ClientApiKeyAttribute>(context) || HasServiceFilter<ClientApiKeyAuthorizationFilter>(context))
            && !HasNoClientApiKeyNeededAttribute(context))
        {
            securityRequirements.Add(CreateSecurityRequirement("Client API Key"));
        }

        if (HasApiKeyAttribute<InterHostApiKeyAttribute>(context) || HasServiceFilter<InterHostApiKeyAuthorizationFilter>(context))
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

    private static bool HasApiKeyAttribute<TAttribute>(OperationFilterContext context) where TAttribute : Attribute
    {
        if (context.MethodInfo.GetCustomAttributes(true).OfType<TAttribute>().Any())
        {
            return true;
        }

        if (context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<TAttribute>().Any() == true)
        {
            return true;
        }

        return context.ApiDescription.ActionDescriptor.EndpointMetadata?.OfType<TAttribute>().Any() == true;
    }

    private static bool HasNoClientApiKeyNeededAttribute(OperationFilterContext context)
    {
        return context.ApiDescription.ActionDescriptor.EndpointMetadata
            ?.OfType<ClientApiKey.NoClientApiKeyNeededAttribute>().Any() == true;
    }

    private static bool HasServiceFilter<TFilter>(OperationFilterContext context)
    {
        return context.ApiDescription.ActionDescriptor.EndpointMetadata?
            .OfType<ServiceFilterAttribute>()
            .Any(filter => filter.ServiceType == typeof(TFilter)) == true;
    }

    private static OpenApiSecurityRequirement CreateSecurityRequirement(string schemeName)
    {
        return new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference(schemeName, SecurityReferenceDocument, null),
                []
            }
        };
    }

    private static OpenApiDocument CreateSecurityReferenceDocument()
    {
        var openApiDocument = new OpenApiDocument
        {
            Components = new OpenApiComponents()
        };

        openApiDocument.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["API Key"] = new OpenApiSecurityScheme
            {
                Name = ApiKey.ApiKeyHeaderName,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "ApiKeyScheme"
            },
            ["Client API Key"] = new OpenApiSecurityScheme
            {
                Name = ClientApiKey.APIKeyHeaderName,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "ClientApiKeyScheme"
            },
            ["Inter Host API Key"] = new OpenApiSecurityScheme
            {
                Name = InterHostApiKey.ApiKeyHeaderName,
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "InterHostApiKeyScheme"
            }
        };

        return openApiDocument;
    }
}