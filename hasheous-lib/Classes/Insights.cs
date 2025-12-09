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
    /// Provides methods and types for generating and recording API usage insights, including reporting and opt-out mechanisms.
    /// </summary>
    public class Insights
    {
        static bool pruneDatabase = false;

        /// <summary>
        /// Specifies the source type for an insight event, such as hash lookups, submissions, or metadata proxy actions.
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
        /// The HTTP header name used by clients to indicate opt-out preferences for API insights logging.
        /// </summary>
        public const string OptOutHeaderName = "X-Insight-Opt-Out";

        /// <summary>
        /// Specifies the types of opt-out options available for API insights logging.
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

            string appWhereClause = "";
            if (appId > 0)
            {
                appWhereClause = " AND client_id = @appId";
            }

            // get unique visitors for the last 30 days
            sql = @"
                SELECT 
                    COUNT(DISTINCT remote_ip) AS unique_visitors,
                    SUM(total_requests) AS total_requests,
                    ROUND(AVG(average_execution_time_ms), 2) AS average_response_time
                FROM
                    Insights_API_Requests_Daily
                WHERE
                    event_datetime >= @startdate AND event_datetime <= @enddate
                        " + appWhereClause + ";";
            DataTable uniqueVisitorsTable = await db.ExecuteCMDAsync(sql, dbDict, 90);
            if (uniqueVisitorsTable.Rows.Count > 0)
            {
                report["unique_visitors"] = uniqueVisitorsTable.Rows[0]["unique_visitors"];
                report["total_requests"] = uniqueVisitorsTable.Rows[0]["total_requests"];
                report["average_response_time"] = uniqueVisitorsTable.Rows[0]["average_response_time"];
            }
            else
            {
                report["unique_visitors"] = 0;
                report["total_requests"] = 0;
                report["average_response_time"] = 0;
            }

            // load countries into a dictionary for mapping
            sql = "SELECT Code, Value FROM Country;";
            DataTable countryTable = await db.ExecuteCMDAsync(sql);
            Dictionary<string, string> countryDict = new Dictionary<string, string>();
            foreach (DataRow row in countryTable.Rows)
            {
                countryDict[row["Code"].ToString() ?? ""] = row["Value"].ToString() ?? "";
            }

            // get unique visitors per country for the last 30 days
            sql = @"
                SELECT 
                    country,
                    COUNT(DISTINCT remote_ip) AS unique_visitors
                FROM
                    Insights_API_Requests_Daily
                WHERE
                    event_datetime >= @startdate AND event_datetime <= @enddate
                        " + appWhereClause + @"
                GROUP BY country
                ORDER BY unique_visitors DESC LIMIT 5;";
            DataTable uniqueVisitorsPerCountryTable = await db.ExecuteCMDAsync(sql, dbDict, 90);
            List<Dictionary<string, object>> uniqueVisitorsPerCountry = new List<Dictionary<string, object>>();
            foreach (DataRow row in uniqueVisitorsPerCountryTable.Rows)
            {
                string countryName = row["country"].ToString() ?? "Unknown";
                if (countryDict.ContainsKey(countryName))
                {
                    countryName = countryDict[countryName];
                }
                else
                {
                    countryName = "Unknown";
                }

                uniqueVisitorsPerCountry.Add(new Dictionary<string, object>
                {
                    { "country", countryName },
                    { "unique_visitors", row["unique_visitors"] }
                });
            }
            report["unique_visitors_per_country"] = uniqueVisitorsPerCountry;

            if (appId > 0)
            {
                // get unique visitors of each client api key for the last 30 days
                sql = @"
                SELECT 
                    ClientAPIKeys.`Name`, apidata.unique_visitors
                FROM
                    ClientAPIKeys
                        JOIN
                    (SELECT 
                        client_apikey_id,
                            COUNT(DISTINCT remote_ip) AS unique_visitors
                    FROM
                        Insights_API_Requests_Daily
                    WHERE
                        event_datetime >= @startdate
                            AND event_datetime <= @enddate
                            AND client_apikey_id IN (SELECT 
                                ClientIdIndex
                            FROM
                                ClientAPIKeys
                            WHERE
                                DataObjectId = @appId)) apidata ON ClientAPIKeys.ClientIdIndex = apidata.client_apikey_id";
                DataTable uniqueVisitorsPerApiKeyTable = await db.ExecuteCMDAsync(sql, dbDict, 90);
                List<Dictionary<string, object>> uniqueVisitorsPerApiKey = new List<Dictionary<string, object>>();
                foreach (DataRow row in uniqueVisitorsPerApiKeyTable.Rows)
                {
                    uniqueVisitorsPerApiKey.Add(new Dictionary<string, object>
                {
                    { "client_apikey_id", row["Name"] },
                    { "unique_visitors", row["unique_visitors"] }
                });
                }
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
        /// Aggregates API request insights into hourly summary data. It processes the last 40 days of whole hours of data (example: 1am - 2am). If the data for an hour has already been aggregated to the Insights_API_Requests_Hourly table, it skips that hour.
        /// </summary>
        /// <returns></returns>
        public static async Task AggregateHourlySummary()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            // find the time 24 hours ago, rounded down to the nearest hour
            DateTime now = DateTime.UtcNow;

            // loop through the last 40 days of whole hours
            for (int i = 1; i <= 960; i++)
            {
                // define the hour range
                DateTime hourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(-i);
                DateTime hourEnd = hourStart.AddHours(1);
                // make sure hourEnd is not in the future
                if (hourEnd > now)
                {
                    continue;
                }

                // check if this hour has already been aggregated
                string checkSql = @"
                    SELECT COUNT(*) AS count
                    FROM Insights_API_Requests_Hourly
                    WHERE event_datetime = @hourStart;";
                Dictionary<string, object> checkParams = new Dictionary<string, object>
                {
                    { "@hourStart", hourStart }
                };
                DataTable checkTable = await db.ExecuteCMDAsync(checkSql, checkParams);
                if (checkTable.Rows.Count > 0 && Convert.ToInt32(checkTable.Rows[0]["count"]) > 0)
                {
                    // this hour has already been aggregated, skip it
                    continue;
                }

                // aggregate data for this hour
                string aggregateSql = @"
                    SELECT 
                        insightType, 
                        remote_ip, 
                        user_id, 
                        country, 
                        client_id, 
                        client_apikey_id, 
                        COUNT(*) AS total_requests, 
                        AVG(execution_time_ms) AS average_response_time 
                    FROM Insights_API_Requests 
                    WHERE event_datetime >= @hourStart AND event_datetime < @hourEnd 
                    GROUP BY insightType, remote_ip, user_id, country, client_id, client_apikey_id;";
                Dictionary<string, object> aggregateParams = new Dictionary<string, object>
                {
                    { "hourStart", hourStart },
                    { "hourEnd", hourEnd }
                };
                DataTable aggregateTable = await db.ExecuteCMDAsync(aggregateSql, aggregateParams);
                // insert aggregated data into Insights_API_Requests_Hourly
                foreach (DataRow row in aggregateTable.Rows)
                {
                    string insertSql = @"
                        INSERT INTO Insights_API_Requests_Hourly
                            (event_datetime, insightType, remote_ip, user_id, country, client_id, client_apikey_id, total_requests, average_execution_time_ms)
                        VALUES
                            (@hourStart, @insightType, @remote_ip, @user_id, @country, @client_id, @client_apikey_id, @total_requests, @average_response_time);";
                    Dictionary<string, object> insertParams = new Dictionary<string, object>
                    {
                        { "@hourStart", hourStart },
                        { "@insightType", row["insightType"] },
                        { "@remote_ip", row["remote_ip"] },
                        { "@user_id", row["user_id"] },
                        { "@country", row["country"] },
                        { "@client_id", row["client_id"] },
                        { "@client_apikey_id", row["client_apikey_id"] },
                        { "@total_requests", row["total_requests"] },
                        { "@average_response_time", row["average_response_time"] }
                    };
                    _ = await db.ExecuteCMDAsync(insertSql, insertParams);
                }
            }

            if (pruneDatabase)
            {
                // drop aggregated data from Insights_API_Requests older than 40 days
                string deleteSql = @"
                DELETE FROM Insights_API_Requests
                WHERE event_datetime < @deleteBefore LIMIT 1000;";
                Dictionary<string, object> deleteParams = new Dictionary<string, object>
                {
                    { "@deleteBefore", now.AddDays(-40) }
                };
                // keep deleting in batches of 1000 until no more rows to delete
                int rowsDeleted;
                do
                {
                    Logging.Log(Logging.LogType.Information, "Insights", "Pruning old Insights_API_Requests data older than 40 days...");
                    DataTable deleteResult = await db.ExecuteCMDAsync(deleteSql, deleteParams);
                    rowsDeleted = deleteResult.Rows.Count;
                } while (rowsDeleted > 0);
            }
        }

        /// <summary>
        /// Aggregates API request insights into daily summary data. Intended to process and summarize daily API usage statistics. Compiles data from the Insights_API_Requests_Hourly table into daily aggregates stored in the Insights_API_Requests_Daily table. Processes the last 35 days of hourly data. If the data for that day has already been aggregated to the Insights_API_Requests_Daily table, it skips that day. Does not process the current day.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task AggregateDailySummary()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            DateTime now = DateTime.UtcNow;

            // loop through the last 35 days
            for (int i = 1; i <= 35; i++)
            {
                DateTime dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-i);
                DateTime dayEnd = dayStart.AddDays(1);
                // make sure dayEnd is before 00:00 UTC of the current day
                if (dayEnd >= new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc))
                {
                    continue;
                }

                // check if this day has already been aggregated
                string checkSql = @"
                    SELECT COUNT(*) AS count
                    FROM Insights_API_Requests_Daily
                    WHERE event_datetime = @dayStart;";
                Dictionary<string, object> checkParams = new Dictionary<string, object>
                {
                    { "@dayStart", dayStart }
                };
                DataTable checkTable = await db.ExecuteCMDAsync(checkSql, checkParams);
                if (checkTable.Rows.Count > 0 && Convert.ToInt32(checkTable.Rows[0]["count"]) > 0)
                {
                    // this day has already been aggregated, skip it
                    continue;
                }

                // aggregate data for this day
                string aggregateSql = @"
                    SELECT 
                        insightType, 
                        remote_ip, 
                        user_id,  
                        country, 
                        client_id, 
                        client_apikey_id, 
                        SUM(total_requests) AS total_requests, 
                        AVG(average_execution_time_ms) AS average_response_time 
                    FROM 
                        Insights_API_Requests_Hourly 
                    WHERE 
                        event_datetime >= @dayStart 
                        AND event_datetime < @dayEnd 
                    GROUP BY insightType, remote_ip, user_id, country, client_id, client_apikey_id;";
                Dictionary<string, object> aggregateParams = new Dictionary<string, object>
                {
                    { "dayStart", dayStart },
                    { "dayEnd", dayEnd }
                };
                DataTable aggregateTable = await db.ExecuteCMDAsync(aggregateSql, aggregateParams);
                // insert aggregated data into Insights_API_Requests_Daily
                foreach (DataRow row in aggregateTable.Rows)
                {
                    string insertSql = @"
                        INSERT INTO Insights_API_Requests_Daily
                            (event_datetime, insightType, remote_ip, user_id, country, client_id, client_apikey_id, total_requests, average_execution_time_ms)
                        VALUES
                            (@dayStart, @insightType, @remote_ip, @user_id, @country, @client_id, @client_apikey_id, @total_requests, @average_response_time);";
                    Dictionary<string, object> insertParams = new Dictionary<string, object>
                    {
                        { "@dayStart", dayStart },
                        { "@insightType", row["insightType"] },
                        { "@remote_ip", row["remote_ip"] },
                        { "@user_id", row["user_id"] },
                        { "@country", row["country"] },
                        { "@client_id", row["client_id"] },
                        { "@client_apikey_id", row["client_apikey_id"] },
                        { "@total_requests", row["total_requests"] },
                        { "@average_response_time", row["average_response_time"] }
                    };
                    _ = await db.ExecuteCMDAsync(insertSql, insertParams);
                }
            }

            if (pruneDatabase)
            {
                // drop aggregated data from Insights_API_Requests_Hourly older than 40 days
                string deleteSql = @"
                DELETE FROM Insights_API_Requests_Hourly
                WHERE event_datetime < @deleteBefore;";
                Dictionary<string, object> deleteParams = new Dictionary<string, object>
                {
                    { "@deleteBefore", now.AddDays(-40) }
                };
                _ = await db.ExecuteCMDAsync(deleteSql, deleteParams);
            }
        }

        /// <summary>
        /// Aggregates API request insights into monthly summary data. Intended to process and summarize monthly API usage statistics.
        /// This method should compile data from the Insights_API_Requests_Daily table into monthly aggregates stored in the Insights_API_Requests_Monthly table. Processes the last 12 months of daily data. If the data for that month has already been aggregated to the Insights_API_Requests_Monthly table, it skips that month. Does not process the current month.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task AggregateMonthlySummary()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            DateTime now = DateTime.UtcNow;

            // loop through the last 12 months
            for (int i = 1; i <= 12; i++)
            {
                DateTime monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
                DateTime monthEnd = monthStart.AddMonths(1);
                // make sure monthEnd is before the first day of the current month
                if (monthEnd >= new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc))
                {
                    continue;
                }

                // check if this month has already been aggregated
                string checkSql = @"
                    SELECT COUNT(*) AS count
                    FROM Insights_API_Requests_Monthly
                    WHERE event_datetime = @monthStart;";
                Dictionary<string, object> checkParams = new Dictionary<string, object>
                {
                    { "@monthStart", monthStart }
                };
                DataTable checkTable = await db.ExecuteCMDAsync(checkSql, checkParams);
                if (checkTable.Rows.Count > 0 && Convert.ToInt32(checkTable.Rows[0]["count"]) > 0)
                {
                    // this month has already been aggregated, skip it
                    continue;
                }

                // aggregate data for this month
                string aggregateSql = @"
                    SELECT 
                        insightType, 
                        remote_ip, 
                        user_id,  
                        country, 
                        client_id, 
                        client_apikey_id, 
                        SUM(total_requests) AS total_requests, 
                        AVG(average_execution_time_ms) AS average_response_time 
                    FROM 
                        Insights_API_Requests_Daily 
                    WHERE 
                        event_datetime >= @monthStart 
                        AND event_datetime < @monthEnd 
                    GROUP BY insightType, remote_ip, user_id, country, client_id, client_apikey_id;";
                Dictionary<string, object> aggregateParams = new Dictionary<string, object>
                {
                    { "monthStart", monthStart },
                    { "monthEnd", monthEnd }
                };
                DataTable aggregateTable = await db.ExecuteCMDAsync(aggregateSql, aggregateParams);
                // insert aggregated data into Insights_API_Requests_Monthly
                foreach (DataRow row in aggregateTable.Rows)
                {
                    string insertSql = @"
                        INSERT INTO Insights_API_Requests_Monthly
                            (event_datetime, insightType, remote_ip, user_id, country, client_id, client_apikey_id, total_requests, average_execution_time_ms)
                        VALUES
                            (@monthStart, @insightType, @remote_ip, @user_id, @country, @client_id, @client_apikey_id, @total_requests, @average_response_time);";
                    Dictionary<string, object> insertParams = new Dictionary<string, object>
                    {
                        { "@monthStart", monthStart },
                        { "@insightType", row["insightType"] },
                        { "@remote_ip", row["remote_ip"] },
                        { "@user_id", row["user_id"] },
                        { "@country", row["country"] },
                        { "@client_id", row["client_id"] },
                        { "@client_apikey_id", row["client_apikey_id"] },
                        { "@total_requests", row["total_requests"] },
                        { "@average_response_time", row["average_response_time"] }
                    };
                    _ = await db.ExecuteCMDAsync(insertSql, insertParams);
                }
            }

            if (pruneDatabase)
            {
                // drop aggregated data from Insights_API_Requests_Daily older than 6 months
                string deleteSql = @"
                DELETE FROM Insights_API_Requests_Daily
                WHERE event_datetime < @deleteBefore;";
                Dictionary<string, object> deleteParams = new Dictionary<string, object>
                {
                    { "@deleteBefore", now.AddMonths(-6) }
                };
                _ = await db.ExecuteCMDAsync(deleteSql, deleteParams);

                // drop aggregated data from Insights_API_Requests_Monthly older than 1 year
                deleteSql = @"
                DELETE FROM Insights_API_Requests_Monthly
                WHERE event_datetime < @deleteBefore;";
                deleteParams = new Dictionary<string, object>
                {
                    { "@deleteBefore", now.AddYears(-1) }
                };
                _ = await db.ExecuteCMDAsync(deleteSql, deleteParams);
            }
        }
    }

    /// <summary>
    /// Attribute for logging API usage insights on controller actions or classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class InsightAttribute : Attribute, IAsyncActionFilter
    {
        /// <summary>
        /// Gets the type of insight source for this attribute, indicating the context in which the insight is logged.
        /// </summary>
        public InsightSourceType InsightSource { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InsightAttribute"/> class with the specified insight source type.
        /// </summary>
        /// <param name="insightSource">The type of insight source for this attribute, indicating the context in which the insight is logged.</param>
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