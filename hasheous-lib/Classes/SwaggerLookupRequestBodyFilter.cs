using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class LookupRequestBodyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
        {
            return;
        }

        if (controllerActionDescriptor.ControllerName != "Lookup" || controllerActionDescriptor.ActionName != "LookupPost")
        {
            return;
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "Raw JSON body containing either one hash object or an array of hash objects. Each object may contain crc, md5, sha1, and/or sha256 fields.",
            Content =
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        OneOf = new List<OpenApiSchema>
                        {
                            new OpenApiSchema
                            {
                                Type = "object",
                                AdditionalProperties = new OpenApiSchema
                                {
                                    Type = "string",
                                    Nullable = true
                                },
                                Example = new OpenApiObject
                                {
                                    ["crc"] = new OpenApiString("12ec7f82"),
                                    ["md5"] = new OpenApiString("5d7550788a4d1b47ad81fbbbf5c615a9"),
                                    ["sha1"] = new OpenApiString("274ed5c2ea2ddc855f67d4c4e61c9d9b7eb68403"),
                                    ["sha256"] = new OpenApiString("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
                                }
                            },
                            new OpenApiSchema
                            {
                                Type = "array",
                                Items = new OpenApiSchema
                                {
                                    Type = "object",
                                    AdditionalProperties = new OpenApiSchema
                                    {
                                        Type = "string",
                                        Nullable = true
                                    }
                                },
                                Example = new OpenApiArray
                                {
                                    new OpenApiObject
                                    {
                                        ["crc"] = new OpenApiString("12ec7f82")
                                    },
                                    new OpenApiObject
                                    {
                                        ["md5"] = new OpenApiString("5d7550788a4d1b47ad81fbbbf5c615a9")
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}