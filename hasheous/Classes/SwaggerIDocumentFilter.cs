using System.Text.Json;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Controllers.v1_0;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class IGDBMetadataDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument openApiDocument, DocumentFilterContext context)
    {
        // loop all classes in IGDB.Models namespace and add them to the document
        var igdbAssembly = typeof(IGDB.Models.Game).Assembly;
        var hasheousAssembly = typeof(HasheousClient.Models.Metadata.IGDB.Game).Assembly;
        foreach (var type in igdbAssembly.GetTypes().Where(t => t.Namespace != null && t.Namespace.StartsWith("IGDB.Models") && t.IsClass))
        {
            // get the igdb endpoint for this type
            var method = typeof(hasheous_server.Classes.Metadata.IGDB.Metadata)
                .GetMethod("GetEndpointData")
                ?.MakeGenericMethod(type);
            hasheous_server.Classes.Metadata.IGDB.Metadata.EndpointDataItem? endpointDataItem;
            try
            {
                endpointDataItem = method?.Invoke(null, null) as hasheous_server.Classes.Metadata.IGDB.Metadata.EndpointDataItem;
            }
            catch
            {
                endpointDataItem = null;
            }

            // check if the type is in IGDB.Models namespace
            if (type.Namespace != null)
            {
                // define operation
                var operation = new OpenApiOperation
                {
                    Summary = $"Get {type.Name}metadata from IGDB.",
                    OperationId = $"Get{type.Name}Metadata"
                };

                // check if the type has a description
                if (!string.IsNullOrEmpty(endpointDataItem?.Endpoint))
                {
                    operation.Description = $"Get {type.Name}metadata from IGDB. See [IGDB API documentation](https://api-docs.igdb.com/#{endpointDataItem.Endpoint}) for more details.";
                }

                // assign tag
                operation.Tags.Add(new OpenApiTag { Name = "MetadataProxy" });

                // set parameters
                operation.Parameters = new List<OpenApiParameter>
                    {
                        new OpenApiParameter
                        {
                            Name = "Id",
                            In = ParameterLocation.Query,
                            Description = $"The ID of the {type.Name} to retrieve.",
                            Required = false,
                            Schema = new OpenApiSchema
                            {
                                Type = "integer($int64)"
                            }
                        }
                    };
                if (endpointDataItem != null && endpointDataItem.SupportsSlugSearch)
                {
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = "slug",
                        In = ParameterLocation.Query,
                        Description = $"The slug of the {type.Name} to retrieve.",
                        Required = false,
                        Schema = new OpenApiSchema
                        {
                            Type = "string"
                        }
                    });
                }

                // look for expandColumns parameter
                bool hasExpandColumns = false;

                // create response properties
                var properties = new Dictionary<string, OpenApiSchema>
                {
                    { type.Name, new OpenApiSchema() { Type = type.Name } }
                };

                // create new object of type, and serialize it to get the properties
                var hasheousType = hasheousAssembly.GetType($"HasheousClient.Models.Metadata.IGDB.{type.Name}");
                if (hasheousType == null)
                {
                    hasheousType = type;
                }
                var instance = Activator.CreateInstance(hasheousType);
                List<string> example = new List<string>();
                if (instance != null)
                {
                    foreach (var property in instance.GetType().GetProperties())
                    {
                        // create a string containing the property type
                        // and description
                        string propertyType = property.PropertyType.Name;

                        properties[property.Name] = new OpenApiSchema
                        {
                            Type = propertyType,
                            Description = propertyType,
                            AdditionalPropertiesAllowed = true
                        };

                        // if the property is a collection, set the type to array
                        if (property.PropertyType.IsGenericType && (property.PropertyType.GetGenericTypeDefinition() == typeof(List<>) || property.PropertyType.GetGenericTypeDefinition() == typeof(IList<>) || property.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>) || property.PropertyType.GetGenericTypeDefinition() == typeof(Array)))
                        {
                            properties[property.Name].Type = "array";
                            properties[property.Name].Items = new OpenApiSchema
                            {
                                Type = property.PropertyType.GetGenericArguments()[0].Name
                            };

                            // get the JsonPropertyName attribute if it exists
                            var jsonPropertyName = property.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), false)
                                .FirstOrDefault() as System.Text.Json.Serialization.JsonPropertyNameAttribute;

                            if (jsonPropertyName != null)
                            {
                                if (!example.Contains(jsonPropertyName.Name))
                                {
                                    example.Add(jsonPropertyName.Name);
                                }
                            }

                            hasExpandColumns = true;
                        }

                        // if the property is a dictionary, set the type to object
                        if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                        {
                            properties[property.Name].Type = "object";
                            properties[property.Name].AdditionalPropertiesAllowed = true;
                        }
                    }
                }
                else
                {
                    // if instance is null, we can still add the type as a property
                    properties[type.Name] = new OpenApiSchema
                    {
                        Type = type.Name,
                        Description = $"An instance of {type.Name}."
                    };
                }

                if (hasExpandColumns)
                {
                    // sort the example list
                    example.Sort();

                    // add expandColumns parameter
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = "expandColumns",
                        In = ParameterLocation.Query,
                        Description = "A comma-separated list of columns to expand in the response. If not provided, only a list of object id's will be returned. Allowed values: " + String.Join(", ", example),
                        Required = false,
                        Schema = new OpenApiSchema
                        {
                            Type = "string"
                        }
                    });
                }

                // create 200 response
                var response200 = new OpenApiResponse
                {
                    Description = "Success"
                };

                // add response type
                response200.Content.Add("application/json", new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = type.FullName,
                        AdditionalPropertiesAllowed = true,
                        Properties = properties,
                        Description = $"Returns the {type.Name} object from IGDB."
                    }
                });

                // adding response to operation
                operation.Responses.Add("200", response200);

                // create 400 response
                var response400 = new OpenApiResponse
                {
                    Description = "Error: Invalid search parameters"
                };

                // add response type
                response400.Content.Add("application/json", new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Description = $"Invalid search parameters for {type.Name}.",
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            { "error", new OpenApiSchema { Type = "string", Description = "Error message" } },
                            { "code", new OpenApiSchema { Type = "integer", Description = "Error code" } }
                        }
                    }
                });

                // adding response to operation
                operation.Responses.Add("400", response400);

                // create 404 response
                var response404 = new OpenApiResponse
                {
                    Description = "Error: Search object not found"
                };

                // adding response to operation
                operation.Responses.Add("404", response404);

                // create 500 response
                var response500 = new OpenApiResponse
                {
                    Description = "Error: Server error"
                };

                // adding response to operation
                operation.Responses.Add("500", response500);

                // enable this code if your endpoint requires authorization.
                List<string> securityRequirements = ["API Key"];

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

                // create path item
                var pathItem = new OpenApiPathItem();
                // add operation to the path
                pathItem.AddOperation(OperationType.Get, operation);
                // finally add the path to document
                openApiDocument?.Paths.Add($"/api/v1/MetadataProxy/IGDB/{type.Name}", pathItem);
            }
        }
    }
}