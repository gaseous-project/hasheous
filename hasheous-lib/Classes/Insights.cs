using System;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Authentication;
using hasheous.Classes;
using hasheous_server.Models;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using static Classes.Insights.Insights;

namespace Classes.Insights
{
    /// <summary>
    /// Provides methods and attributes for collecting and reporting API usage insights, including opt-out handling and reporting.
    /// </summary>
    public class Insights
    {
        /// <summary>
        /// Specifies the source type for an insight event, such as hash lookup, submission, or metadata proxy.
        /// </summary>
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

        /// <summary>
        /// The HTTP header name used to indicate opt-out preferences for insights collection.
        /// </summary>
        public const string OptOutHeaderName = "X-Insight-Opt-Out";

        /// <summary>
        /// Specifies the types of opt-out options available for insights collection.
        /// </summary>
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
            string cacheKey = RedisConnection.GenerateKey("InsightsReport", appId);
            // check if the query is cached
            if (Config.RedisConfiguration.Enabled)
            {
                string? cachedData = RedisConnection.GetDatabase(0).StringGet(cacheKey);
                if (cachedData != null)
                {
                    // if cached data is found, deserialize it and return
                    var deserializedData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedData);
                    return deserializedData ?? new Dictionary<string, object>();
                }
            }

            Dictionary<string, object> report = new Dictionary<string, object>();

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object> dbDict = new Dictionary<string, object>
            {
                { "@appId", appId },
                { "@startdate", DateTime.UtcNow.AddDays(-30) },
                { "@enddate", DateTime.UtcNow  }
            };


            // --- AGGREGATION-AWARE LOGIC ---
            // We'll query the summary table for all days except today, and the raw table for today.
            // Then, merge the results for each metric.

            // 1. True unique visitors (distinct IPs across all days in last 30 days)
            string monthIpSql = @"
                SELECT DISTINCT remote_ip
                FROM Insights_API_Requests
                WHERE event_datetime >= @startdate AND event_datetime <= @enddate" + (appId > 0 ? " AND client_id = @appId" : "") + ";";
            DataTable monthIpTable = await db.ExecuteCMDAsync(monthIpSql, dbDict);
            long uniqueVisitorsMonth = monthIpTable.Rows.Count;

            // Total requests and average response time (last 30 days)
            string totalSql = @"
                SELECT COUNT(*) AS total_requests, AVG(execution_time_ms) AS average_response_time
                FROM Insights_API_Requests
                WHERE event_datetime >= @startdate AND event_datetime <= @enddate" + (appId > 0 ? " AND client_id = @appId" : "") + ";";
            DataTable totalTable = await db.ExecuteCMDAsync(totalSql, dbDict);
            long totalRequests = 0;
            double avgResponseTime = 0;
            if (totalTable.Rows.Count > 0)
            {
                totalRequests = totalTable.Rows[0]["total_requests"] != DBNull.Value ? Convert.ToInt64(totalTable.Rows[0]["total_requests"]) : 0;
                avgResponseTime = totalTable.Rows[0]["average_response_time"] != DBNull.Value ? Convert.ToDouble(totalTable.Rows[0]["average_response_time"]) : 0;
            }
            report["unique_visitors"] = uniqueVisitorsMonth;
            report["total_requests"] = totalRequests;
            report["average_response_time"] = avgResponseTime;

            // // 1b. Unique visitors per day (last 30 days)
            // var uniqueVisitorsPerDay = new List<Dictionary<string, object>>();
            // for (int i = 0; i < 30; i++)
            // {
            //     DateTime day = DateTime.UtcNow.Date.AddDays(-i);
            //     string daySql = @"
            //         SELECT COUNT(DISTINCT remote_ip) AS unique_visitors
            //         FROM Insights_API_Requests
            //         WHERE DATE(event_datetime) = @day" + (appId > 0 ? " AND client_id = @appId" : "") + ";";
            //     var dayParams = new Dictionary<string, object>(dbDict) { ["@day"] = day };
            //     DataTable dayTable = await db.ExecuteCMDAsync(daySql, dayParams);
            //     long dayCount = 0;
            //     if (dayTable.Rows.Count > 0)
            //     {
            //         dayCount = dayTable.Rows[0]["unique_visitors"] != DBNull.Value ? Convert.ToInt64(dayTable.Rows[0]["unique_visitors"]) : 0;
            //     }
            //     uniqueVisitorsPerDay.Add(new Dictionary<string, object>
            //     {
            //         { "date", day.ToString("yyyy-MM-dd") },
            //         { "unique_visitors", dayCount }
            //     });
            // }
            // report["unique_visitors_per_day"] = uniqueVisitorsPerDay;

            // 2. Country mapping
            sql = "SELECT Code, Value FROM Country;";
            DataTable countryTable = await db.ExecuteCMDAsync(sql);
            Dictionary<string, string> countryDict = new Dictionary<string, string>();
            foreach (DataRow row in countryTable.Rows)
            {
                countryDict[row["Code"].ToString() ?? ""] = row["Value"].ToString() ?? "";
            }

            // 3. Unique visitors per country (last 30 days)
            // Summary table (excluding today)
            string summaryWhere = appId > 0 ? " AND client_id = @appId" : "";
            Dictionary<string, object> summaryParams = new Dictionary<string, object>(dbDict)
            {
                ["@today"] = DateTime.UtcNow.Date
            };
            string summaryCountrySql = @"
                SELECT country, SUM(unique_visitors) AS unique_visitors
                FROM Insights_API_Requests_DailySummary
                WHERE summary_date >= @startdate AND summary_date < @today" + summaryWhere + @"
                GROUP BY country
                ORDER BY unique_visitors DESC LIMIT 5;";
            DataTable summaryCountryTable = await db.ExecuteCMDAsync(summaryCountrySql, summaryParams);

            // Raw table (today only)
            string rawWhere = appId > 0 ? " AND client_id = @appId" : "";
            Dictionary<string, object> rawParams = new Dictionary<string, object>(dbDict)
            {
                ["@today"] = DateTime.UtcNow.Date
            };
            string rawCountrySql = @"
                SELECT country, COUNT(DISTINCT remote_ip) AS unique_visitors
                FROM Insights_API_Requests
                WHERE event_datetime >= @today AND event_datetime <= @enddate" + rawWhere + @"
                GROUP BY country
                ORDER BY unique_visitors DESC LIMIT 5;";
            DataTable rawCountryTable = await db.ExecuteCMDAsync(rawCountrySql, rawParams);

            // Merge per country
            var countryCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in summaryCountryTable.Rows)
            {
                string country = row["country"].ToString() ?? "Unknown";
                long count = row["unique_visitors"] != DBNull.Value ? Convert.ToInt64(row["unique_visitors"]) : 0;
                if (countryCounts.ContainsKey(country))
                    countryCounts[country] += count;
                else
                    countryCounts[country] = count;
            }
            foreach (DataRow row in rawCountryTable.Rows)
            {
                string country = row["country"].ToString() ?? "Unknown";
                long count = row["unique_visitors"] != DBNull.Value ? Convert.ToInt64(row["unique_visitors"]) : 0;
                if (countryCounts.ContainsKey(country))
                    countryCounts[country] += count;
                else
                    countryCounts[country] = count;
            }
            // Top 5
            var uniqueVisitorsPerCountry = countryCounts
                .OrderByDescending(kv => kv.Value)
                .Take(5)
                .Select(kv => new Dictionary<string, object>
                {
                    { "country", countryDict.ContainsKey(kv.Key) ? countryDict[kv.Key] : "Unknown" },
                    { "unique_visitors", kv.Value }
                })
                .ToList();
            report["unique_visitors_per_country"] = uniqueVisitorsPerCountry;

            // 4. Unique visitors per API key (last 30 days, only if appId > 0)
            if (appId > 0)
            {
                // Summary table (excluding today)
                string summaryApiKeySql = @"
                    SELECT client_apikey_id, SUM(unique_visitors) AS unique_visitors
                    FROM Insights_API_Requests_DailySummary
                    WHERE summary_date >= @startdate AND summary_date < @today AND client_id = @appId
                    GROUP BY client_apikey_id;";
                DataTable summaryApiKeyTable = await db.ExecuteCMDAsync(summaryApiKeySql, summaryParams);

                // Raw table (today only)
                string rawApiKeySql = @"
                    SELECT client_apikey_id, COUNT(DISTINCT remote_ip) AS unique_visitors
                    FROM Insights_API_Requests
                    WHERE event_datetime >= @today AND event_datetime <= @enddate AND client_id = @appId
                    GROUP BY client_apikey_id;";
                DataTable rawApiKeyTable = await db.ExecuteCMDAsync(rawApiKeySql, rawParams);

                // Merge per API key
                var apiKeyCounts = new Dictionary<long, long>();
                foreach (DataRow row in summaryApiKeyTable.Rows)
                {
                    long id = row["client_apikey_id"] != DBNull.Value ? Convert.ToInt64(row["client_apikey_id"]) : 0;
                    long count = row["unique_visitors"] != DBNull.Value ? Convert.ToInt64(row["unique_visitors"]) : 0;
                    if (apiKeyCounts.ContainsKey(id))
                        apiKeyCounts[id] += count;
                    else
                        apiKeyCounts[id] = count;
                }
                foreach (DataRow row in rawApiKeyTable.Rows)
                {
                    long id = row["client_apikey_id"] != DBNull.Value ? Convert.ToInt64(row["client_apikey_id"]) : 0;
                    long count = row["unique_visitors"] != DBNull.Value ? Convert.ToInt64(row["unique_visitors"]) : 0;
                    if (apiKeyCounts.ContainsKey(id))
                        apiKeyCounts[id] += count;
                    else
                        apiKeyCounts[id] = count;
                }
                // Map API key IDs to names
                var apiKeyNames = new Dictionary<long, string>();
                string apiKeyNameSql = "SELECT ClientIdIndex, Name FROM ClientAPIKeys WHERE DataObjectId = @appId;";
                DataTable apiKeyNameTable = await db.ExecuteCMDAsync(apiKeyNameSql, new Dictionary<string, object> { ["@appId"] = appId });
                foreach (DataRow row in apiKeyNameTable.Rows)
                {
                    long id = row["ClientIdIndex"] != DBNull.Value ? Convert.ToInt64(row["ClientIdIndex"]) : 0;
                    string name = row["Name"].ToString() ?? "";
                    apiKeyNames[id] = name;
                }
                var uniqueVisitorsPerApiKey = apiKeyCounts.Select(kv => new Dictionary<string, object>
                {
                    { "client_apikey_id", apiKeyNames.ContainsKey(kv.Key) ? apiKeyNames[kv.Key] : kv.Key.ToString() },
                    { "unique_visitors", kv.Value }
                }).ToList();
                report["unique_visitors_per_api_key"] = uniqueVisitorsPerApiKey;
            }

            // cache the result
            if (Config.RedisConfiguration.Enabled)
            {
                hasheous.Classes.RedisConnection.GetDatabase(0).StringSet(cacheKey, Newtonsoft.Json.JsonConvert.SerializeObject(report), TimeSpan.FromMinutes(30));
            }

            return report;
        }

        /// <summary>
        /// Aggregates the previous day's API request data into the daily summary table.
        /// Should be called by a scheduled job (e.g., orchestrator) once per day.
        /// </summary>
        /// <returns>True if aggregation succeeded, false otherwise.</returns>
        public static async Task<bool> AggregateDailySummary()
        {
            try
            {
                var db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                // Get all distinct dates in the table except today
                string getDatesSql = @"SELECT DISTINCT DATE(event_datetime) AS summary_date FROM Insights_API_Requests WHERE event_datetime < CURDATE() ORDER BY summary_date ASC;";
                DataTable datesTable = await db.ExecuteCMDAsync(getDatesSql);
                bool allSucceeded = true;
                foreach (DataRow row in datesTable.Rows)
                {
                    DateTime day = (DateTime)row["summary_date"];
                    string dayStr = day.ToString("yyyy-MM-dd");

                    // Aggregate unique_visitors by counting distinct remote_ip per (date, client_id, insightType, country)
                    // This ensures unique_visitors is not overcounted when multiple IPs exist per group
                    string aggregateSql = @"
                        INSERT INTO Insights_API_Requests_DailySummary (
                            summary_date, client_id, insightType, country, unique_visitors, total_requests, average_response_time
                        )
                        SELECT
                            @summary_date AS summary_date,
                            client_id,
                            insightType,
                            country,
                            COUNT(DISTINCT remote_ip) AS unique_visitors,
                            COUNT(*) AS total_requests,
                            AVG(execution_time_ms) AS average_response_time
                        FROM Insights_API_Requests
                        WHERE DATE(event_datetime) = @summary_date
                        GROUP BY client_id, insightType, country
                        ON DUPLICATE KEY UPDATE
                            unique_visitors = VALUES(unique_visitors),
                            total_requests = VALUES(total_requests),
                            average_response_time = VALUES(average_response_time);
                    ";
                    var aggregateParams = new Dictionary<string, object> { { "@summary_date", dayStr } };
                    try
                    {
                        _ = await db.ExecuteCMDAsync(aggregateSql, aggregateParams);
                    }
                    catch (Exception exAgg)
                    {
                        Logging.Log(Logging.LogType.Warning, "Insights.AggregateDailySummary", $"Aggregation failed for {dayStr}", exAgg);
                        allSucceeded = false;
                        continue;
                    }

                    // // Delete raw data for this day using parameter
                    // string deleteSql = @"
                    //     DELETE FROM Insights_API_Requests WHERE DATE(event_datetime) = @summary_date;
                    // ";
                    // var deleteParams = new Dictionary<string, object> { { "@summary_date", dayStr } };
                    // try
                    // {
                    //     await db.ExecuteCMDAsync(deleteSql, deleteParams);
                    // }
                    // catch (Exception exDel)
                    // {
                    //     Logging.Log(Logging.LogType.Warning, "Insights.AggregateDailySummary", $"Cleanup (delete) failed for {dayStr}", exDel);
                    //     allSucceeded = false;
                    // }
                }
                return allSucceeded;
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "Insights.AggregateDailySummary", "Aggregation failed", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Attribute for logging API usage insights on controller actions or classes.
    /// Applies insight collection logic, including opt-out handling, for decorated endpoints.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class InsightAttribute : Attribute, IAsyncActionFilter
    {
        /// <summary>
        /// Gets the source type for the insight event, such as hash lookup, submission, or metadata proxy.
        /// </summary>
        public InsightSourceType InsightSource { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InsightAttribute"/> class with the specified insight source type.
        /// </summary>
        /// <param name="insightSource">The source type for the insight event, such as hash lookup, submission, or metadata proxy.</param>
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