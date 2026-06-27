using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
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

        var requestBody = new OpenApiRequestBody
        {
            Required = true,
            Description = "Raw JSON body containing either one hash object or an array of hash objects. Each object may contain crc, md5, sha1, and/or sha256 fields."
        };

        var content = new Dictionary<string, OpenApiMediaType>();
        content["application/json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                OneOf = new List<IOpenApiSchema>
                {
                    new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        AdditionalProperties = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String | JsonSchemaType.Null
                        },
                        Example = JsonNode.Parse("{\"crc\":\"12ec7f82\",\"md5\":\"5d7550788a4d1b47ad81fbbbf5c615a9\",\"sha1\":\"274ed5c2ea2ddc855f67d4c4e61c9d9b7eb68403\",\"sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}")
                    },
                    new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            AdditionalProperties = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String | JsonSchemaType.Null
                            }
                        },
                        Example = JsonNode.Parse("[{\"crc\":\"12ec7f82\"},{\"md5\":\"5d7550788a4d1b47ad81fbbbf5c615a9\"}]")
                    }
                }
            }
        };
        requestBody.Content = content;

        operation.RequestBody = requestBody;
    }
}