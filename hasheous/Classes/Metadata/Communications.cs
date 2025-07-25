using System;
using System.Data;
using System.Net;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;
using RestEase;

namespace hasheous_server.Classes.Metadata
{
    /// <summary>
    /// Handles all metadata API communications
    /// </summary>
    public class Communications
    {
        public Communications(MetadataSources Source)
        {
            MetadataSource = Source;
        }

        private static IGDBClient igdb = new IGDBClient(
                    // Found in Twitch Developer portal for your app
                    Config.IGDB.ClientId,
                    Config.IGDB.Secret
                );

        /// <summary>
        /// Configure metadata API communications
        /// </summary>
        public MetadataSources MetadataSource
        {
            get
            {
                return _MetadataSource;
            }
            set
            {
                _MetadataSource = value;

                switch (value)
                {
                    case MetadataSources.IGDB:
                        // set rate limiter avoidance values
                        RateLimitAvoidanceWait = 1500;
                        RateLimitAvoidanceThreshold = 3;
                        RateLimitAvoidancePeriod = 1;

                        // set rate limiter recovery values
                        RateLimitRecoveryWaitTime = 10000;

                        break;
                    default:
                        // leave all values at default
                        break;
                }
            }
        }
        private MetadataSources _MetadataSource = MetadataSources.None;

        // rate limit avoidance - what can we do to ensure that rate limiting is avoided?
        // these values affect all communications

        /// <summary>
        /// How long to wait to avoid hitting an API rate limiter
        /// </summary>
        private static int RateLimitAvoidanceWait = 2000;

        /// <summary>
        /// How many API calls in the period are allowed before we start introducing a wait
        /// </summary>
        private static int RateLimitAvoidanceThreshold = 80;

        /// <summary>
        /// A counter of API calls since the beginning of the period
        /// </summary>
        private static int RateLimitAvoidanceCallCount = 0;

        /// <summary>
        /// How large the period (in seconds) to measure API call counts against
        /// </summary>
        private static int RateLimitAvoidancePeriod = 60;

        /// <summary>
        /// The start of the rate limit avoidance period
        /// </summary>
        private static DateTime RateLimitAvoidanceStartTime = DateTime.UtcNow;

        /// <summary>
        /// Used to determine if we're already in rate limit avoidance mode - always query "InRateLimitAvoidanceMode"
        /// for up to date mode status.
        /// This bool is used to track status changes and should not be relied upon for current status.
        /// </summary>
        private static bool InRateLimitAvoidanceModeStatus = false;

        /// <summary>
        /// Determine if we're in rate limit avoidance mode.
        /// </summary>
        private static bool InRateLimitAvoidanceMode
        {
            get
            {
                if (RateLimitAvoidanceStartTime.AddSeconds(RateLimitAvoidancePeriod) <= DateTime.UtcNow)
                {
                    // avoidance period has expired - reset
                    RateLimitAvoidanceCallCount = 0;
                    RateLimitAvoidanceStartTime = DateTime.UtcNow;

                    return false;
                }
                else
                {
                    // we're in the avoidance period
                    if (RateLimitAvoidanceCallCount > RateLimitAvoidanceThreshold)
                    {
                        // the number of call counts indicates we should throttle things a bit
                        if (InRateLimitAvoidanceModeStatus == false)
                        {
                            Logging.Log(Logging.LogType.Information, "API Connection", "Entered rate limit avoidance period, API calls will be throttled by " + RateLimitAvoidanceWait + " milliseconds.");
                            InRateLimitAvoidanceModeStatus = true;
                        }
                        return true;
                    }
                    else
                    {
                        // still in full speed mode - no throttle required
                        if (InRateLimitAvoidanceModeStatus == true)
                        {
                            Logging.Log(Logging.LogType.Information, "API Connection", "Exited rate limit avoidance period, API call rate is returned to full speed.");
                            InRateLimitAvoidanceModeStatus = false;
                        }
                        return false;
                    }
                }
            }
        }

        // rate limit handling - how long to wait to allow the server to recover and try again
        // these values affect ALL communications if a 429 response code is received

        /// <summary>
        /// How long to wait (in milliseconds) if a 429 status code is received before trying again
        /// </summary>
        private static int RateLimitRecoveryWaitTime = 10000;

        /// <summary>
        /// The time when normal communications can attempt to be resumed
        /// </summary>
        private static DateTime RateLimitResumeTime = DateTime.UtcNow.AddMinutes(5 * -1);

        // rate limit retry - how many times to retry before aborting
        private int RetryAttempts = 0;
        private int RetryAttemptsMax = 3;

        /// <summary>
        /// Supported metadata sources
        /// </summary>
        public enum MetadataSources
        {
            /// <summary>
            /// None - always returns null for metadata requests - should not really be using this source
            /// </summary>
            None,

            /// <summary>
            /// IGDB - queries the IGDB service for metadata
            /// </summary>
            IGDB,

            /// <summary>
            /// TheGamesDb - queries TheGamesDb service for metadata
            /// </summary>
            TheGamesDb,

            /// <summary>
            /// RetroAchievements - queries RetroAchievements service for metadata
            /// </summary>
            RetroAchievements,

            /// <summary>
            /// GiantBomb - queries GiantBomb service for metadata
            /// </summary>
            GiantBomb,

            /// <summary>
            /// Steam - queries Steam service for metadata
            /// </summary>
            Steam,

            /// <summary>
            /// GOG - queries GOG service for metadata
            /// </summary>
            GOG,

            /// <summary>
            /// EpicGameStore - queries Epic Game Store service for metadata
            /// </summary>
            EpicGameStore,

            /// <summary>
            /// Wikipedia - queries Wikipedia service for metadata
            /// </summary>
            Wikipedia,

            /// <summary>
            /// SteamGridDb - queries SteamGridDb service for metadata
            /// </summary>
            SteamGridDb
        }

        /// <summary>
        /// Request data from the metadata API
        /// </summary>
        /// <typeparam name="T">Type of object to return</typeparam>
        /// <param name="Endpoint">API endpoint segment to use</param>
        /// <param name="Fields">Fields to request from the API</param>
        /// <param name="Query">Selection criteria for data to request</param>
        /// <returns></returns>
        public async Task<T[]?> APIComm<T>(string Endpoint, string Fields, string Query)
        {
            switch (_MetadataSource)
            {
                case MetadataSources.None:
                    return null;
                case MetadataSources.IGDB:
                    return await IGDBAPI<T>(Endpoint, Fields, Query);

                default:
                    return null;
            }
        }

        private async Task<T[]> IGDBAPI<T>(string Endpoint, string Fields, string Query)
        {
            Logging.Log(Logging.LogType.Debug, "API Connection", "Accessing API for endpoint: " + Endpoint);

            if (RateLimitResumeTime > DateTime.UtcNow)
            {
                Logging.Log(Logging.LogType.Information, "API Connection", "IGDB rate limit hit. Pausing API communications until " + RateLimitResumeTime.ToString() + ". Attempt " + RetryAttempts + " of " + RetryAttemptsMax + " retries.");
                Thread.Sleep(RateLimitRecoveryWaitTime);
            }

            try
            {
                if (InRateLimitAvoidanceMode == true)
                {
                    // sleep for a moment to help avoid hitting the rate limiter
                    Thread.Sleep(RateLimitAvoidanceWait);
                }

                // perform the actual API call
                string queryString = Fields + " " + Query + ";";
                var results = await igdb.QueryAsync<T>(Endpoint, query: queryString);

                // increment rate limiter avoidance call count
                RateLimitAvoidanceCallCount += 1;

                return results;
            }
            catch (ApiException apiEx)
            {
                switch (apiEx.StatusCode)
                {
                    case HttpStatusCode.TooManyRequests:
                        if (RetryAttempts >= RetryAttemptsMax)
                        {
                            Logging.Log(Logging.LogType.Warning, "API Connection", "IGDB rate limiter attempts expired. Aborting.", apiEx);
                            throw;
                        }
                        else
                        {
                            Logging.Log(Logging.LogType.Information, "API Connection", "IGDB API rate limit hit while accessing endpoint " + Endpoint, apiEx);

                            RetryAttempts += 1;

                            return await IGDBAPI<T>(Endpoint, Fields, Query);
                        }
                    default:
                        Logging.Log(Logging.LogType.Warning, "API Connection", "Exception when accessing endpoint " + Endpoint, apiEx);
                        throw;
                }
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "API Connection", "Exception when accessing endpoint " + Endpoint, ex);
                throw;
            }
        }

        public static async Task<T?> GetSearchCache<T>(string SearchFields, string SearchString)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM SearchCache WHERE SearchFields = @searchfields AND SearchString = @searchstring;";
            Dictionary<string, object> dbDict = new Dictionary<string, object>
            {
                { "searchfields", SearchFields },
                { "searchstring", SearchString }
            };
            DataTable data = await db.ExecuteCMDAsync(sql, dbDict);
            if (data.Rows.Count > 0)
            {
                // cache hit
                string rawString = data.Rows[0]["Content"].ToString();
                T ReturnValue = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(rawString);
                if (ReturnValue != null)
                {
                    Logging.Log(Logging.LogType.Information, "Search Cache", "Found search result in cache. Search string: " + SearchString);
                    return ReturnValue;
                }
                else
                {
                    Logging.Log(Logging.LogType.Information, "Search Cache", "Search result not found in cache.");
                    return default;
                }
            }
            else
            {
                // cache miss
                Logging.Log(Logging.LogType.Information, "Search Cache", "Search result not found in cache.");
                return default;
            }
        }

        public static void SetSearchCache<T>(string SearchFields, string SearchString, T SearchResult)
        {
            Logging.Log(Logging.LogType.Information, "Search Cache", "Storing search results in cache. Search string: " + SearchString);

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "INSERT INTO SearchCache (SearchFields, SearchString, Content, LastSearch) VALUES (@searchfields, @searchstring, @content, @lastsearch);";
            Dictionary<string, object> dbDict = new Dictionary<string, object>
            {
                { "searchfields", SearchFields },
                { "searchstring", SearchString },
                { "content", Newtonsoft.Json.JsonConvert.SerializeObject(SearchResult) },
                { "lastsearch", DateTime.UtcNow }
            };
            db.ExecuteNonQuery(sql, dbDict);
        }
    }
}