using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TaskRunner.Classes
{
    /// <summary>
    /// Provides HTTP client helper methods for making API requests with authentication and custom headers.
    /// </summary>
    public static class HttpHelper
    {
        /// <summary>
        /// Gets or sets the base URI for all HTTP requests. Setting this value rebuilds the request headers.
        /// </summary>
        public static string BaseUri
        {
            get
            {
                return client.BaseAddress != null ? client.BaseAddress.ToString() : string.Empty;
            }
            set
            {
                client.BaseAddress = new Uri(value);

                BuildHeaders();
            }
        }

        private static void BuildHeaders()
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true
            };

            // add client supplied headers
            if (Headers != null)
            {
                foreach (KeyValuePair<string, string> header in Headers)
                {
                    if (client.DefaultRequestHeaders.Contains(header.Key))
                    {
                        client.DefaultRequestHeaders.Remove(header.Key);
                    }
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }
        }

        /// <summary>
        /// Gets or sets additional HTTP headers to be included in requests.
        /// </summary>
        public static Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        private static HttpClient client = CreateHttpClient();

        /// <summary>
        /// Creates and configures an HttpClient instance. In debug mode, configures to trust self-signed certificates.
        /// </summary>
        /// <returns>A configured HttpClient instance.</returns>
        private static HttpClient CreateHttpClient()
        {
#if DEBUG
            // In debug mode, trust self-signed certificates (e.g., Kestrel development certificates)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            return new HttpClient(handler);
#else
            return new HttpClient();
#endif
        }

        /// <summary>
        /// Sends a POST request with JSON content to the specified URL and deserializes the response.
        /// </summary>
        /// <typeparam name="T?">The type to deserialize the response into.</typeparam>
        /// <param name="url">The URL to send the POST request to.</param>
        /// <param name="contentValue">The object to serialize as JSON in the request body.</param>
        /// <returns>The deserialized response object of type T.</returns>
        public static async Task<T?> Post<T>(string url, object contentValue)
        {
            // ensure headers are built
            BuildHeaders();

            var stringContent = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(contentValue), Encoding.UTF8, "application/json");
            await stringContent.LoadIntoBufferAsync();
            var response = await client.PostAsync(url, stringContent);
            response.EnsureSuccessStatusCode();

            // Deserialize the updated product from the response body.
            var resultStr = await response.Content.ReadAsStringAsync();
            var resultObject = JsonConvert.DeserializeObject<T>(resultStr, new JsonSerializerSettings
            {
                Converters = { new SafeEnumConverter() }
            });

            return resultObject;
        }

        /// <summary>
        /// Sends a PUT request with JSON content to the specified URL and deserializes the response.
        /// </summary>
        /// <typeparam name="T?">The type to deserialize the response into.</typeparam>
        /// <param name="url">The URL to send the PUT request to.</param>
        /// <param name="contentValue">The object to serialize as JSON in the request body.</param>
        /// <returns>The deserialized response object of type T.</returns>
        public static async Task<T?> Put<T>(string url, object contentValue)
        {
            // ensure headers are built
            BuildHeaders();

            var stringContent = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(contentValue), Encoding.UTF8, "application/json");
            await stringContent.LoadIntoBufferAsync();
            var response = await client.PutAsync(url, stringContent);
            response.EnsureSuccessStatusCode();

            // Deserialize the updated product from the response body.
            var resultStr = await response.Content.ReadAsStringAsync();
            var resultObject = JsonConvert.DeserializeObject<T>(resultStr, new JsonSerializerSettings
            {
                Converters = { new SafeEnumConverter() }
            });

            return resultObject;
        }

        /// <summary>
        /// Sends a GET request to the specified URL and deserializes the response.
        /// </summary>
        /// <typeparam name="T?">The type to deserialize the response into.</typeparam>
        /// <param name="url">The URL to send the GET request to.</param>
        /// <returns>The deserialized response object of type T.</returns>
        public static async Task<T?> Get<T>(string url)
        {
            // ensure headers are built
            BuildHeaders();

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Get the response
            string resultStr = await response.Content.ReadAsStringAsync();

            // Deserialize the response to T
            T? resultObject = JsonConvert.DeserializeObject<T>(resultStr, new JsonSerializerSettings
            {
                MaxDepth = 8,
                ObjectCreationHandling = ObjectCreationHandling.Auto,
                CheckAdditionalContent = true,
                Converters = { new SafeEnumConverter() }
            });

            return resultObject;
        }

        /// <summary>
        /// Sends a DELETE request to the specified URL and ensures a successful HTTP response.
        /// </summary>
        /// <param name="url">The URL to send the DELETE request to.</param>
        public static async Task Delete(string url)
        {
            // ensure headers are built
            BuildHeaders();

            var response = await client.DeleteAsync(url);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Custom JSON converter that safely handles unknown enum values by falling back to an "Unknown" value or default.
        /// </summary>
        public class SafeEnumConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                var t = Nullable.GetUnderlyingType(objectType) ?? objectType;
                return t.IsEnum;
            }

            /// <summary>
            /// Reads JSON and converts it to an enum value, falling back to "Unknown" or default if the value is not recognized.
            /// </summary>
            /// <param name="reader">The JSON reader.</param>
            /// <param name="objectType">The target enum type.</param>
            /// <param name="existingValue">The existing value (unused).</param>
            /// <param name="serializer">The JSON serializer.</param>
            /// <returns>The deserialized enum value or fallback.</returns>
            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                var isNullable = Nullable.GetUnderlyingType(objectType) != null;
                var enumType = Nullable.GetUnderlyingType(objectType) ?? objectType;
                try
                {
                    if (reader.TokenType == JsonToken.String)
                    {
                        var enumText = reader.Value?.ToString();
                        if (Enum.TryParse(enumType, enumText, true, out var enumValue))
                        {
                            return enumValue;
                        }
                    }
                    else if (reader.TokenType == JsonToken.Integer)
                    {
                        var intValue = Convert.ToInt32(reader.Value);
                        if (Enum.IsDefined(enumType, intValue))
                        {
                            return Enum.ToObject(enumType, intValue);
                        }
                    }
                }
                catch { }

                // Fallback to Unknown if present, else default
                var names = Enum.GetNames(enumType);
                if (names.Contains("Unknown"))
                {
                    return Enum.Parse(enumType, "Unknown");
                }
                return isNullable ? null : Activator.CreateInstance(enumType);
            }

            /// <summary>
            /// Writes an enum value as JSON.
            /// </summary>
            /// <param name="writer">The JSON writer.</param>
            /// <param name="value">The enum value to write.</param>
            /// <param name="serializer">The JSON serializer.</param>
            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                writer.WriteValue(value?.ToString());
            }
        }
    }
}