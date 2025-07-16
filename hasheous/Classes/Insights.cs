using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Authentication;
using hasheous_server.Models;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity;
using static Classes.Insights.Insights;

namespace Classes.Insights
{
    public class Insights
    {
        public enum InsightSourceType
        {
            /// <summary>
            /// Undefined source type, used when no specific source is defined.
            /// This is the default value and should be used when the source is not applicable or unknown.
            /// </summary>
            Undefined = 0,

            /// <summary>
            /// Represents an insight source for hash lookups.
            /// This is used when the action is related to looking up hashes in the database.
            /// </summary>
            /// <remarks>
            /// This source type is used for actions that retrieve information based on hash values.
            /// </remarks>
            HashLookup = 1,

            /// <summary>
            /// Represents an insight source for hash submissions.
            /// This is used when the action involves submitting hashes to the database.
            /// </summary>
            /// <remarks>
            /// This source type is used for actions that involve adding new hash signatures to the database.
            /// </remarks>
            HashSubmission = 2,

            /// <summary>
            /// Represents an insight source for metadata proxy actions.
            /// This is used when the action involves interacting with metadata proxies.
            /// </summary>
            /// <remarks>
            /// This source type is used for actions that retrieve or manipulate metadata through a proxy service.
            /// </remarks>
            MetadataProxy = 3,

            /// <summary>
            /// Represents an insight source for deprecated hash lookups.
            /// This is used when the action is related to looking up hashes in a deprecated manner.
            /// </summary>
            /// <remarks>
            /// This source type is used for actions that retrieve information based on hash values but are considered legacy or outdated.
            /// </remarks>
            HashLookupDeprecated = 10
        }

        public const string OptOutHeaderName = "X-Insight-Opt-Out";

        public enum OptOutType
        {
            /// <summary>
            /// The user has not opted out of insights (default)
            /// </summary>
            NotOptedOut,

            /// <summary>
            /// Opt out of all insights
            /// </summary>
            BlockAll,

            /// <summary>
            /// Opt out of storing IP addresses in insights
            /// </summary>
            BlockIP,

            /// <summary>
            /// Opt out of storing user information in insights
            /// </summary>
            BlockUser,

            /// <summary>
            /// Opt out of storing location information in insights
            /// </summary>
            BlockLocation
        }

        /// <summary>
        /// Generates an insight report for the specified application ID.
        /// The report includes unique visitors, total requests, average response time, and more.
        /// </summary>
        /// <param name="appId">The application ID for which to generate the report.</param>
        /// <returns>A dictionary containing the insight report data.</returns>
        /// <remarks>
        /// This method retrieves data from the Insights_API_Requests table and aggregates it to provide insights
        /// such as unique visitors, requests per country, total requests, average response time, and unique visitors per API key.
        /// The report is generated for the last 30 days.
        /// </remarks>
        public async static Task<Dictionary<string, object>> GenerateInsightReport(long appId)
        {
            Dictionary<string, object> report = new Dictionary<string, object>();

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict = new Dictionary<string, object>
            {
                { "@appId", appId }
            };

            // get unique visitors for the last 30 days
            sql = @"
                SELECT 
                    COUNT(DISTINCT remote_ip) AS unique_visitors
                FROM
                    Insights_API_Requests
                WHERE
                    event_datetime >= NOW() - INTERVAL 30 DAY
                        AND client_id = @appId;";
            DataTable uniqueVisitorsTable = await db.ExecuteCMDAsync(sql, dbDict);
            if (uniqueVisitorsTable.Rows.Count > 0)
            {
                report["unique_visitors"] = uniqueVisitorsTable.Rows[0]["unique_visitors"];
            }
            else
            {
                report["unique_visitors"] = 0;
            }

            // get unique visitors per country for the last 30 days
            sql = @"
                SELECT 
                    CASE
                        WHEN Country.Value IS NULL THEN Insights_API_Requests.country
                        ELSE Country.Value
                    END AS Country,
                    COUNT(DISTINCT remote_ip) AS unique_visitors
                FROM
                    Insights_API_Requests
                        LEFT JOIN
                    Country ON Insights_API_Requests.country = Country.Code
                WHERE
                    event_datetime >= NOW() - INTERVAL 30 DAY
                        AND client_id = @appId
                GROUP BY country;";
            DataTable uniqueVisitorsPerCountryTable = await db.ExecuteCMDAsync(sql, dbDict);
            List<Dictionary<string, object>> uniqueVisitorsPerCountry = new List<Dictionary<string, object>>();
            foreach (DataRow row in uniqueVisitorsPerCountryTable.Rows)
            {
                uniqueVisitorsPerCountry.Add(new Dictionary<string, object>
                {
                    { "country", row["country"] },
                    { "unique_visitors", row["unique_visitors"] }
                });
            }
            report["unique_visitors_per_country"] = uniqueVisitorsPerCountry;

            // get total requests for the last 30 days
            sql = @"
                SELECT 
                    COUNT(*) AS total_requests
                FROM
                    Insights_API_Requests
                WHERE
                    event_datetime >= NOW() - INTERVAL 30 DAY
                        AND client_id = @appId;";
            DataTable totalRequestsTable = await db.ExecuteCMDAsync(sql, dbDict);
            if (totalRequestsTable.Rows.Count > 0)
            {
                report["total_requests"] = totalRequestsTable.Rows[0]["total_requests"];
            }
            else
            {
                report["total_requests"] = 0;
            }

            // get average response time
            sql = @"
                SELECT 
                    AVG(execution_time_ms) AS average_response_time
                FROM
                    Insights_API_Requests
                WHERE
                    event_datetime >= NOW() - INTERVAL 30 DAY
                        AND client_id = @appId;";
            DataTable averageResponseTimeTable = await db.ExecuteCMDAsync(sql, dbDict);
            if (averageResponseTimeTable.Rows.Count > 0)
            {
                report["average_response_time"] = averageResponseTimeTable.Rows[0]["average_response_time"];
            }
            else
            {
                report["average_response_time"] = 0;
            }

            // get unique visitors of each client api key for the last 30 days
            sql = @"
                SELECT 
                    ClientAPIKeys.Name AS Country,
                    COUNT(DISTINCT remote_ip) AS unique_visitors
                FROM
                    Insights_API_Requests
                JOIN
                    ClientAPIKeys ON Insights_API_Requests.client_apikey_id = ClientAPIKeys.ClientIdIndex AND ClientAPIKeys.DataObjectId = @appId
                WHERE
                    event_datetime >= NOW() - INTERVAL 30 DAY
                        AND client_id = @appId
                GROUP BY ClientAPIKeys.Name
                ORDER BY ClientAPIKeys.Name;";
            DataTable uniqueVisitorsPerApiKeyTable = await db.ExecuteCMDAsync(sql, dbDict);
            List<Dictionary<string, object>> uniqueVisitorsPerApiKey = new List<Dictionary<string, object>>();
            foreach (DataRow row in uniqueVisitorsPerApiKeyTable.Rows)
            {
                uniqueVisitorsPerApiKey.Add(new Dictionary<string, object>
                {
                    { "client_apikey_id", row["Country"] },
                    { "unique_visitors", row["unique_visitors"] }
                });
            }
            report["unique_visitors_per_api_key"] = uniqueVisitorsPerApiKey;

            // get events per minute for the last day
            sql = @"
                SELECT 
                    DATE_FORMAT(event_datetime, '%Y-%m-%d %H:%i') AS time,
                    COUNT(*) AS events
                FROM
                    Insights_API_Requests
                WHERE
                    event_datetime >= NOW() - INTERVAL 1 DAY
                        AND client_id = @appId
                GROUP BY time
                ORDER BY time DESC;";
            DataTable eventsPerMinuteTable = await db.ExecuteCMDAsync(sql, dbDict);
            List<Dictionary<string, object>> eventsPerMinute = new List<Dictionary<string, object>>();
            foreach (DataRow row in eventsPerMinuteTable.Rows)
            {
                eventsPerMinute.Add(new Dictionary<string, object>
                {
                    { "time", row["time"] },
                    { "events", row["events"] }
                });
            }
            report["events_per_minute"] = eventsPerMinute;

            return report;
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class InsightAttribute : Attribute, IAsyncActionFilter
    {
        public InsightSourceType InsightSource { get; }

        public InsightAttribute(InsightSourceType insightSource = InsightSourceType.Undefined)
        {
            InsightSource = insightSource;
        }

        /// <summary>
        /// Called asynchronously before and after the action executes, allowing you to log or modify the request/response.
        /// </summary>
        /// <param name="context">The context for the action executing.</param>
        /// <param name="next">The delegate to execute the next action filter or the action itself.</param>
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;

            // Check if the user has opted out of insights
            List<OptOutType> optOutTypes = new List<OptOutType>();
            if (httpContext.Request.Headers.TryGetValue(OptOutHeaderName, out var optOutValue))
            {
                // Parse the opt-out value
                string[] optOutValues = optOutValue.ToString().Split(',');
                foreach (string value in optOutValues)
                {
                    if (Enum.TryParse(value.Trim(), true, out OptOutType optOutType))
                    {
                        optOutTypes.Add(optOutType);
                    }
                }
            }

            // If the user has opted out of all insights, skip logging
            if (optOutTypes.Contains(OptOutType.BlockAll))
            {
                await next();
                return;
            }

            // Get HTTP method (GET, POST, etc.)
            string httpMethod = httpContext.Request.Method;

            // Get remote IP
            string remoteIp = "";
            if (optOutTypes.Contains(OptOutType.BlockIP))
            {
                // If the user has opted out of storing IP addresses, set it to "unknown"
                remoteIp = "unknown";
            }
            else if (httpContext.Request.Headers.ContainsKey("true-client-ip"))
            {
                // If behind a proxy, use the X-Forwarded-For header
                remoteIp = httpContext.Request.Headers["true-client-ip"].ToString();
            }
            else if (httpContext.Request.Headers.ContainsKey("CF-Connecting-IPv6"))
            {
                // If behind a proxy, use the X-Forwarded-For header
                remoteIp = httpContext.Request.Headers["CF-Connecting-IPv6"].ToString();
            }
            else if (httpContext.Request.Headers.ContainsKey("cf-connecting-ip"))
            {
                // If behind a proxy, use the X-Forwarded-For header
                remoteIp = httpContext.Request.Headers["cf-connecting-ip"].ToString();
            }
            else if (httpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                // If behind a proxy, use the X-Forwarded-For header
                remoteIp = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            }
            else if (httpContext.Connection.RemoteIpAddress != null)
            {
                // Otherwise, use the RemoteIpAddress from the connection
                remoteIp = httpContext.Connection.RemoteIpAddress.ToString();
            }
            // If the remote IP is still empty, set it to "unknown"
            if (string.IsNullOrEmpty(remoteIp) && !optOutTypes.Contains(OptOutType.BlockIP))
                // If the user has not opted out of storing IP addresses, set it to "unknown"
                remoteIp = "unknown";

            // hash the remote IP address for privacy
            if (!optOutTypes.Contains(OptOutType.BlockIP) && remoteIp != "unknown")
            {
                // Hash the remote IP address using SHA1
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    byte[] bytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(remoteIp));
                    remoteIp = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                }
            }

            // If the user has opted out of storing location information, skip the location lookup
            string country = "";
            if (!optOutTypes.Contains(OptOutType.BlockLocation))
            {
                if (httpContext.Request.Headers.TryGetValue("cf-ipcountry", out var countryHeader))
                {
                    // If the request contains a cf-ipcountry header, use it
                    country = countryHeader.ToString();
                }
            }

            // Get endpoint address (path)
            string endpoint = httpContext.Request.Path;

            // Start timing
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            await next();

            stopwatch.Stop();
            long executionTimeMs = stopwatch.ElapsedMilliseconds;

            // Get client ID and API key ID from headers
            // This could be a time consuming operation, so it needs to be done after the action execution
            string clientAPIKey = "";
            long clientAPIKeyId = 0;
            long clientId = 0;
            if (httpContext.Request.Headers.TryGetValue(ClientApiKey.APIKeyHeaderName, out var apiKeyValue))
            {
                clientAPIKey = apiKeyValue.ToString();
                ClientApiKey clientApiKeyResolver = new ClientApiKey();
                ClientApiKeyItem? clientApiKeyItem = clientApiKeyResolver.GetAppFromApiKey(clientAPIKey);
                if (clientApiKeyItem != null)
                {
                    clientAPIKeyId = (long)clientApiKeyItem.KeyId;
                    clientId = (long)clientApiKeyItem.ClientAppId;
                }
            }

            // lookup user id from user name if available
            // first check if the user is providing an API key, if not, we will use the UserManager to get the user ID
            string userId = String.Empty;
            // Check if the user has opted out of storing user information
            if (optOutTypes.Contains(OptOutType.BlockUser))
            {
                // If the user has opted out of storing user information, set userId to "unknown"
                userId = "unknown";
            }
            else
            {
                // If the user has not opted out of storing user information, we will try to get the user ID
                if (httpContext.Request.Headers.TryGetValue(ApiKey.ApiKeyHeaderName, out var userIdHeader))
                {
                    ApplicationUser? user = new ApiKey().GetUserFromApiKey(userIdHeader.ToString());
                    if (user != null)
                    {
                        userId = user.Id;
                    }
                }
                else if (httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    // check the cache first
                    if (Config.RedisConfiguration.Enabled)
                    {
                        string? cachedUserId = hasheous.Classes.RedisConnection.GetDatabase(0).StringGet("Insights:User:" + httpContext.User.Identity.Name);
                        if (cachedUserId != null)
                        {
                            userId = cachedUserId;
                        }
                    }

                    // if not cached, use UserManager to get the user ID
                    // This is a more reliable way to get the user ID, especially if the user is authenticated
                    // Note: This requires the UserManager to be registered in the service collection
                    if (string.IsNullOrEmpty(userId))
                    {
                        var userManager = httpContext.RequestServices.GetService<UserManager<ApplicationUser>>();
                        if (userManager != null)
                        {
                            userId = userManager.GetUserId(httpContext.User);

                            // Cache the user ID for future requests
                            if (Config.RedisConfiguration.Enabled)
                            {
                                hasheous.Classes.RedisConnection.GetDatabase(0).StringSet("Insights:User:" + httpContext.User.Identity.Name, userId, TimeSpan.FromHours(1));
                            }
                        }
                    }
                }
            }

            // Insert into DB
            try
            {
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

                string sql = @"
                    INSERT INTO Insights_API_Requests 
                        (
                            event_datetime,
                            insightType,
                            remote_ip,
                            `method`,
                            endpoint_address,
                            execution_time_ms,
                            response_status_code,
                            user_id,
                            user_agent,
                            country,
                            client_id,
                            client_apikey_id
                        )
                    VALUES
                        (
                            CURRENT_TIMESTAMP,
                            @insightType,
                            @remoteip,
                            @method, 
                            @endpoint, 
                            @executionTime, 
                            @responseStatusCode,
                            @user_id,
                            @user_agent,
                            @country,
                            @client_id,
                            @client_apikey_id
                        );";
                Dictionary<string, object> parameters = new Dictionary<string, object>
                    {
                        { "@insightType", (int)InsightSource },
                        { "@remoteip", remoteIp },
                        { "@method", httpMethod },
                        { "@endpoint", endpoint },
                        { "@executionTime", executionTimeMs },
                        { "@responseStatusCode", httpContext.Response.StatusCode },
                        { "@user_id", userId },
                        { "@user_agent", httpContext.Request.Headers["User-Agent"].ToString() ?? "unknown" },
                        { "@country", country },
                        { "@client_id", clientId },
                        { "@client_apikey_id", clientAPIKeyId }
                    };

                _ = await db.ExecuteCMDAsync(sql, parameters);
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "InsightAttribute", "An error occurred while storing insights.", ex);
            }
        }
    }
}