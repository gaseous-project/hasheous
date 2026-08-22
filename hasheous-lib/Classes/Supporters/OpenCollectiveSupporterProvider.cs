using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Classes.Supporters
{
    /// <summary>
    /// Provides OpenCollective contribution data for supporter recognition.
    /// </summary>
    public class OpenCollectiveSupporterProvider : ISupporterProvider
    {
        private const string GraphQlEndpoint = "https://api.opencollective.com/graphql/v2";
        private static readonly HttpClient HttpClient = new HttpClient();

        /// <inheritdoc/>
        public string ProviderName => SupporterConstants.OpenCollectiveProviderName;

        /// <inheritdoc/>
        public bool IsLinkingEnabled =>
            !string.IsNullOrWhiteSpace(Config.SupporterRecognitionConfiguration.OpenCollectiveClientId)
            && !string.IsNullOrWhiteSpace(Config.SupporterRecognitionConfiguration.OpenCollectiveClientSecret);

        /// <inheritdoc/>
        public bool IsSyncEnabled =>
            !string.IsNullOrWhiteSpace(Config.SupporterRecognitionConfiguration.OpenCollectiveApiToken)
            && !string.IsNullOrWhiteSpace(Config.SupporterRecognitionConfiguration.OpenCollectiveCollectiveSlug);

        /// <inheritdoc/>
        public async Task<List<SupporterContributionRecord>> GetRecentContributionsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            if (!IsSyncEnabled)
            {
                return new List<SupporterContributionRecord>();
            }

            string graphQlQuery = """
                query SupporterTransactions($slug: String!, $dateFrom: DateTime!) {
                  account(slug: $slug) {
                    transactions(limit: 1000, type: CREDIT, dateFrom: $dateFrom) {
                      nodes {
                        createdAt
                        fromAccount {
                          id
                          slug
                          name
                        }
                      }
                    }
                  }
                }
                """;

            string payload = JsonSerializer.Serialize(new
            {
                query = graphQlQuery,
                variables = new
                {
                    slug = Config.SupporterRecognitionConfiguration.OpenCollectiveCollectiveSlug,
                    dateFrom = utcNow.AddDays(-Config.SupporterRecognitionConfiguration.ActiveContributionDays).ToString("yyyy-MM-ddTHH:mm:ssZ")
                }
            });

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Config.SupporterRecognitionConfiguration.OpenCollectiveApiToken);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            using JsonDocument document = JsonDocument.Parse(responseContent);
            ThrowIfGraphQlErrorsExist(document);

            List<SupporterContributionRecord> contributions = new List<SupporterContributionRecord>();
            if (!TryGetTransactionsNode(document, out JsonElement nodes))
            {
                return contributions;
            }

            foreach (JsonElement node in nodes.EnumerateArray())
            {
                if (!node.TryGetProperty("fromAccount", out JsonElement fromAccount))
                {
                    continue;
                }

                string? accountId = GetOptionalString(fromAccount, "id");
                DateTime? createdAt = GetOptionalDateTime(node, "createdAt");
                if (string.IsNullOrWhiteSpace(accountId) || createdAt == null)
                {
                    continue;
                }

                contributions.Add(new SupporterContributionRecord
                {
                    Provider = ProviderName,
                    AccountId = accountId,
                    AccountSlug = GetOptionalString(fromAccount, "slug"),
                    DisplayName = GetOptionalString(fromAccount, "name"),
                    LastPaymentUtc = DateTime.SpecifyKind(createdAt.Value, DateTimeKind.Utc)
                });
            }

            return contributions;
        }

        /// <summary>
        /// Throws an exception when the OpenCollective GraphQL response contains one or more errors.
        /// </summary>
        /// <param name="document">The parsed GraphQL response.</param>
        private static void ThrowIfGraphQlErrorsExist(JsonDocument document)
        {
            if (document.RootElement.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                throw new InvalidOperationException("OpenCollective GraphQL request returned one or more errors.");
            }
        }

        /// <summary>
        /// Attempts to locate the transaction node array in a GraphQL response.
        /// </summary>
        /// <param name="document">The parsed GraphQL response.</param>
        /// <param name="nodes">The located transaction node array when present.</param>
        /// <returns><c>true</c> when the node array exists; otherwise, <c>false</c>.</returns>
        private static bool TryGetTransactionsNode(JsonDocument document, out JsonElement nodes)
        {
            nodes = default;
            if (!document.RootElement.TryGetProperty("data", out JsonElement data))
            {
                return false;
            }

            if (!data.TryGetProperty("account", out JsonElement account) || account.ValueKind == JsonValueKind.Null)
            {
                return false;
            }

            if (!account.TryGetProperty("transactions", out JsonElement transactions))
            {
                return false;
            }

            return transactions.TryGetProperty("nodes", out nodes) && nodes.ValueKind == JsonValueKind.Array;
        }

        /// <summary>
        /// Reads an optional string property from a JSON element.
        /// </summary>
        /// <param name="element">The JSON element containing the property.</param>
        /// <param name="propertyName">The property name to read.</param>
        /// <returns>The string value when present; otherwise, <c>null</c>.</returns>
        private static string? GetOptionalString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        /// <summary>
        /// Reads an optional UTC date value from a JSON element.
        /// </summary>
        /// <param name="element">The JSON element containing the property.</param>
        /// <param name="propertyName">The property name to read.</param>
        /// <returns>The parsed UTC date when present; otherwise, <c>null</c>.</returns>
        private static DateTime? GetOptionalDateTime(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return DateTime.TryParse(value.GetString(), out DateTime parsedValue)
                ? parsedValue.ToUniversalTime()
                : null;
        }
    }
}
