using System.Data;
using System.Net;
using Classes;
using Classes.Metadata;
using hasheous_server.Classes.Metadata;
using hasheous_server.Models;
using HasheousClient.WebApp;

namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// ScreenScraper metadata provider that implements <see cref="IMetadata"/> to locate and return
    /// metadata matches from the ScreenScraper source for data objects (companies, platforms, games).
    /// </summary>
    public class MetadataScreenScraper : IMetadata
    {
        /// <summary>
        /// Tracks the last time user information was fetched from the ScreenScraper API to manage API rate limits.
        /// </summary>
        private static DateTime lastUserInfoFetchTime = DateTime.UtcNow.AddHours(-10);

        /// <summary>
        /// Defines the interval in minutes for fetching user information from the ScreenScraper API to check API rate limits.
        /// </summary>
        private static int userInfoFetchIntervalMinutes = 60;

        /// <summary>
        /// Tracks the number of API calls made to the ScreenScraper API to manage API rate limits. Resets to 0 when user information is fetched to check the remaining API calls and reset time. This counter is incremented with each API call made to the ScreenScraper API, and it helps ensure that the application does not exceed the daily limit of 10000 calls. 
        /// </summary>
        private static int apiCallCount = 0;

        /// <summary>
        /// Tracks the number of failed match API calls made to the ScreenScraper API to manage API rate limits. Resets to 0 when user information is fetched to check the remaining API calls and reset time. This counter is incremented with each failed match API call (e.g., when a ROM hash lookup returns a 404 Not Found) made to the ScreenScraper API, and it helps ensure that the application does not exceed the daily limit of 2000 failed calls, which could lead to temporary blocking of the API access. By tracking both successful and failed API calls, the application can better manage its usage of the ScreenScraper API and avoid hitting rate limits.
        /// </summary>
        private static int apiFailedCallCount = 0;

        /// <summary>
        /// Defines the threshold percentage of API calls used to stop making API calls.
        /// </summary>
        private static int maxApiCallsThresholdPercentage = 90;

        /// <summary>
        /// Defines the delay in minutes to wait before retrying API calls after exceeding the API call limit. This delay is used when the application detects that it has approached or exceeded the API call limits based on the user information fetched from the ScreenScraper API. By implementing this delay, the application can avoid making further API calls that would be rejected due to rate limits and can instead wait until the limits are reset before resuming API interactions.
        /// </summary>
        private static int exceededAPICallDelayMinutes = 60;

        /// <summary>
        /// Stores the user information retrieved from the ScreenScraper API, including the number of API calls used and the time until the next reset. This information is used to manage API rate limits by tracking how many calls have been made and when the limits will reset. The user information is fetched at regular intervals defined by <see cref="userInfoFetchIntervalMinutes"/> to ensure that the application has up-to-date information on API usage and can avoid exceeding the limits.
        /// </summary>
        private static ssUser? userItem;

        /// <inheritdoc/>
        public Communications.MetadataSources MetadataSource => Communications.MetadataSources.ScreenScraper;

        /// <inheritdoc/>
        public bool Enabled
        {
            get
            {
                return !String.IsNullOrEmpty(Config.ScreenScraperConfiguration.ClientId) && !String.IsNullOrEmpty(Config.ScreenScraperConfiguration.Secret) && !String.IsNullOrEmpty(Config.ScreenScraperConfiguration.DevClientId) && !String.IsNullOrEmpty(Config.ScreenScraperConfiguration.DevSecret);
            }
        }

        /// <summary>
        /// Connects to the ScreenScraper API and searches for matches based on the ROM hashes in the provided <paramref name="item"/>. The <paramref name="searchCandidates"/> value is ignored for ScreenScraper game metadata matching since it relies on ROM hash matching rather than name-based searching. The <paramref name="options"/> parameter is also ignored for ScreenScraper metadata matching since no additional options are needed for ROM hash searching.
        /// </summary>
        /// <param name="item">The data object item containing ROM hashes to search for.</param>
        /// <param name="searchCandidates">Ignored for ScreenScraper game metadata matching.</param>
        /// <param name="options">Ignored for ScreenScraper game metadata matching.</param>
        /// <returns>A task representing the asynchronous operation, with a <see cref="DataObjects.MatchItem"/> result containing the metadata match.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<DataObjects.MatchItem> FindMatchItemAsync(DataObjectItem item, List<string> searchCandidates, Dictionary<string, object>? options = null)
        {
            hasheous_server.Classes.DataObjects.MatchItem? DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };

            // check if we have the user info
            if (userItem == null || DateTime.UtcNow - lastUserInfoFetchTime > TimeSpan.FromMinutes(userInfoFetchIntervalMinutes))
            {
                // fetch user info from ScreenScraper API
                try
                {
                    UserItem fullUserItem = await HttpHelper.Get<UserItem>(UserItem.Endpoint());
                    userItem = fullUserItem.response.ssuser; // update the user information with the latest data from the API response
                    lastUserInfoFetchTime = DateTime.UtcNow;
                    apiCallCount = 0; // reset API call count after fetching user info
                }
                catch (Exception ex)
                {
                    // Unable to determine remaining quota - treat as rate-limited to be safe.
                    Logging.Log(Logging.LogType.Critical, "ScreenScraper", $"Failed to fetch user info from ScreenScraper API: {ex.Message}");

                    throw new MetadataRateLimitException(
                        $"ScreenScraper: could not fetch user info to check API limits: {ex.Message}",
                        DateTime.UtcNow.AddMinutes(userInfoFetchIntervalMinutes));
                }
            }

            // check if we are approaching the API call limit
            if (userItem != null)
            {
                int maxRequestsPerDay = int.TryParse(userItem.maxrequestsperday, out int maxReq) ? maxReq : 10000; // default to 10000
                int requestsToday = int.TryParse(userItem.requeststoday, out int reqToday) ? reqToday : 0;
                int maxAllowedRequests = (int)(maxRequestsPerDay * (maxApiCallsThresholdPercentage / 100.0));
                int requestsTotal = requestsToday + apiCallCount;
                if (requestsTotal >= maxAllowedRequests)
                {
                    Logging.Log(Logging.LogType.Warning, "ScreenScraper", $"Approaching API call limit: {requestsTotal}/{maxAllowedRequests} calls used. Stopping API calls to avoid exceeding the limit.");
                    throw new MetadataRateLimitException(
                        $"ScreenScraper: daily request limit approached ({requestsTotal}/{maxAllowedRequests}).", DateTime.UtcNow.AddMinutes(exceededAPICallDelayMinutes));
                }

                int maxFailedRequestsPerDay = int.TryParse(userItem.maxrequestskoperday, out int maxFailedReq) ? maxFailedReq : 2000; // default to 2000
                int failedRequestsToday = int.TryParse(userItem.requestskotoday, out int failedReqToday) ? failedReqToday : 0;
                int maxAllowedFailedRequests = (int)(maxFailedRequestsPerDay * (maxApiCallsThresholdPercentage / 100.0));
                int failedRequestsTotal = failedRequestsToday + apiFailedCallCount;
                if (failedRequestsTotal >= maxAllowedFailedRequests)
                {
                    Logging.Log(Logging.LogType.Warning, "ScreenScraper", $"Approaching failed API call limit: {failedRequestsTotal}/{maxAllowedFailedRequests} failed calls used. Stopping API calls to avoid exceeding the limit.");
                    throw new MetadataRateLimitException(
                        $"ScreenScraper: daily failed-request limit approached ({failedRequestsTotal}/{maxAllowedFailedRequests}).", DateTime.UtcNow.AddMinutes(exceededAPICallDelayMinutes));
                }
            }

            switch (item.ObjectType)
            {
                case DataObjects.DataObjectType.Game:
                    DataObjectSearchResults = await GetGameDataAsync(item);
                    break;
                case DataObjects.DataObjectType.Platform:
                    DataObjectSearchResults = await GetPlatformAsync(item, searchCandidates);
                    break;
                default:
                    // ScreenScraper metadata matching is only implemented for games and platforms, so return no match for other types
                    break;
            }

            return DataObjectSearchResults;
        }

        private async Task<DataObjects.MatchItem> GetGameDataAsync(DataObjectItem item)
        {
            hasheous_server.Classes.DataObjects.MatchItem? DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };

            // get the first ROM hashs from the item to search for
            if (item.Attributes?.Find(a => a.attributeName == AttributeItem.AttributeName.ROMs) != null)
            {
                List<Signatures_Games_2.RomItem> romItems = item.Attributes.Find(a => a.attributeName == AttributeItem.AttributeName.ROMs)?.Value as List<Signatures_Games_2.RomItem> ?? new List<Signatures_Games_2.RomItem>();

                foreach (Signatures_Games_2.RomItem romItem in romItems)
                {
                    // first check if we have the hash in our local cache mapping table to avoid unnecessary API calls
                    string sql = "SELECT GameId FROM Screenscraper_HashToGameMap WHERE Hash = @Hash AND HashType = @HashType LIMIT 1";
                    Dictionary<string, object> parameters = new Dictionary<string, object>();

                    string endpointUrl;
                    string hashUsed;
                    if (!string.IsNullOrEmpty(romItem.Md5))
                    {
                        endpointUrl = GameItem.Endpoint(sha1hash: romItem.Sha1);
                        hashUsed = romItem.Md5;
                        parameters["@Hash"] = romItem.Md5;
                        parameters["@HashType"] = "MD5";
                    }
                    else if (!string.IsNullOrEmpty(romItem.Sha1))
                    {
                        endpointUrl = GameItem.Endpoint(sha1hash: romItem.Sha1);
                        hashUsed = romItem.Sha1;
                        parameters["@Hash"] = romItem.Sha1;
                        parameters["@HashType"] = "SHA1";
                    }
                    else
                    {
                        // if we don't have a hash to search for, skip this ROM
                        continue;
                    }

                    // now check if the the hash is in our cache of failed hash lookups to avoid unnecessary API calls for hashes we know won't return results
                    string failedsql = "SELECT COUNT(1) FROM Screenscraper_FailedHashLookups WHERE Hash = @Hash AND HashType = @HashType";
                    DataTable failedLookupResult = await Config.database.ExecuteCMDAsync(failedsql, parameters);
                    if (failedLookupResult.Rows.Count > 0 && int.TryParse(failedLookupResult.Rows[0][0].ToString(), out int failedLookupCount) && failedLookupCount > 0)
                    {
                        // we have a failed lookup for this hash, skip the API call
                        continue;
                    }

                    try
                    {
                        // check the cache first to avoid unnecessary API calls
                        DataTable? cacheResult = await Config.database.ExecuteCMDAsync(sql, parameters);
                        if (cacheResult.Rows.Count > 0)
                        {
                            if (!String.IsNullOrEmpty(cacheResult.Rows[0]["GameId"].ToString()))
                            {
                                long gameId = long.Parse(cacheResult.Rows[0]["GameId"].ToString() ?? "0");
                                if (gameId > 0)
                                {
                                    // we have a cached match, check if we have a copy of the metadata locally, and that it hasn't been updated in the last 30 days
                                    string cacheFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_Screenscraper, "games", gameId.ToString() + ".json");

                                    if (File.Exists(cacheFilePath))
                                    {
                                        // check file info to ensure the file is less than 30 days old
                                        FileInfo fileInfo = new FileInfo(cacheFilePath);
                                        if (fileInfo.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-30))
                                        {
                                            // we have a valid cached file, return the match
                                            DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                            {
                                                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                                MetadataId = gameId.ToString()
                                            };

                                            break; // exit the loop after the first successful match
                                        }
                                        else
                                        {
                                            // cached file is too old, delete it and continue to fetch fresh data
                                            File.Delete(cacheFilePath);
                                        }
                                    }
                                }
                            }
                        }

                        // query the ScreenScraper API for the ROM hash
                        var response = await HttpHelper.Get<GameItem>(endpointUrl);
                        apiCallCount++; // increment API call count after making the call

                        if (response != null && response.response != null && response.response.jeu != null && response.response.jeu.id != null)
                        {
                            // capture the user information from the response to update our API usage tracking in case the user information has changed since we last fetched it
                            if (response.response.ssuser != null)
                            {
                                userItem = response.response.ssuser; // update the user information with the latest data from the API response
                                lastUserInfoFetchTime = DateTime.UtcNow; // update the last fetch time to now since we just got fresh user info from the API
                                apiFailedCallCount = 0; // reset failed API call count after getting a successful response which indicates we are not currently blocked by rate limits
                            }

                            // we have a match, return it
                            DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                            {
                                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                MetadataId = response.response.jeu.id.ToString()
                            };

                            // now cache it
                            string cacheFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_Screenscraper, "games", response.response.jeu.id.ToString() + ".json");
                            if (!File.Exists(cacheFilePath))
                            {
                                // ensure the directory exists
                                Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath) ?? string.Empty);

                                // save the response to the cache file
                                File.WriteAllText(cacheFilePath, Newtonsoft.Json.JsonConvert.SerializeObject(response.response.jeu));

                                // update mapping table
                                // delete existing maps
                                sql = "DELETE FROM Screenscraper_HashToGameMap WHERE GameId = @GameId";
                                _ = await Config.database.ExecuteCMDAsync(sql, new Dictionary<string, object> { { "@GameId", response.response.jeu.id } });

                                // add new maps
                                foreach (var jeuRom in response.response.jeu.roms)
                                {
                                    if (!String.IsNullOrEmpty(jeuRom.romcrc))
                                    {
                                        sql = "INSERT INTO Screenscraper_HashToGameMap (Hash, HashType, GameId) VALUES (@Hash, @HashType, @GameId)";
                                        _ = await Config.database.ExecuteCMDAsync(sql, new Dictionary<string, object> { { "@Hash", jeuRom.romcrc }, { "@HashType", "CRC32" }, { "@GameId", response.response.jeu.id } });
                                    }

                                    if (!String.IsNullOrEmpty(jeuRom.rommd5))
                                    {
                                        sql = "INSERT INTO Screenscraper_HashToGameMap (Hash, HashType, GameId) VALUES (@Hash, @HashType, @GameId)";
                                        _ = await Config.database.ExecuteCMDAsync(sql, new Dictionary<string, object> { { "@Hash", jeuRom.rommd5 }, { "@HashType", "MD5" }, { "@GameId", response.response.jeu.id } });
                                    }

                                    if (!String.IsNullOrEmpty(jeuRom.romsha1))
                                    {
                                        sql = "INSERT INTO Screenscraper_HashToGameMap (Hash, HashType, GameId) VALUES (@Hash, @HashType, @GameId)";
                                        _ = await Config.database.ExecuteCMDAsync(sql, new Dictionary<string, object> { { "@Hash", jeuRom.romsha1 }, { "@HashType", "SHA1" }, { "@GameId", response.response.jeu.id } });
                                    }
                                }
                            }

                            break; // exit the loop after the first successful match
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        switch (httpEx.StatusCode)
                        {
                            case HttpStatusCode.NotFound:
                                // 404 Not Found means we don't have metadata for this ROM hash, so we can cache this result to avoid unnecessary API calls in the future
                                sql = "INSERT INTO Screenscraper_FailedHashLookups (Hash, HashType, LookupDate) VALUES (@Hash, @HashType, @LookupDate)";
                                await Config.database.ExecuteCMDAsync(sql, new Dictionary<string, object> { { "@Hash", hashUsed }, { "@HashType", parameters["@HashType"] }, { "@LookupDate", DateTime.UtcNow } });
                                apiFailedCallCount++; // increment failed API call count since this is a failed lookup which counts against the failed call limit

                                Logging.Log(Logging.LogType.Information, "ScreenScraper", $"No metadata found for ROM hash {hashUsed}. Caching this result to avoid future API calls for this hash.");
                                break;
                            case HttpStatusCode.Unauthorized:
                                Logging.Log(Logging.LogType.Critical, "Screenscraper", "Unauthorized access to ScreenScraper API. Please check your API credentials and ensure they are valid.");
                                break;
                            default:
                                // log the error and continue with the next hash
                                Logging.Log(Logging.LogType.Critical, "ScreenScraper", $"HTTP error querying ScreenScraper API for ROM hash {hashUsed}: {httpEx.Message}");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // log the error and continue with the next hash
                        Logging.Log(Logging.LogType.Critical, "ScreenScraper", $"Error querying ScreenScraper API for ROM hash {hashUsed}: {ex.Message}");
                    }
                }
            }

            return DataObjectSearchResults;
        }

        private async Task<DataObjects.MatchItem> GetPlatformAsync(DataObjectItem item, List<string> SearchCandidates)
        {
            List<ssPlatform>? response = null;

            // check if the cache file exists and is valid - file must be less than 30 days old to be valid
            string cacheFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_Screenscraper, "platforms.json");
            if (File.Exists(cacheFilePath))
            {
                var platformFileInfo = new FileInfo(cacheFilePath);
                if (platformFileInfo.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-30))
                {
                    // cache file is valid, read from it
                    string cachedData = File.ReadAllText(cacheFilePath);
                    response = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ssPlatform>>(cachedData);
                }
                else
                {
                    // cache file is too old, delete it
                    File.Delete(cacheFilePath);
                }
            }

            // if we don't have a valid cache, fetch from the API
            if (response == null)
            {
                try
                {
                    var apiResponse = await HttpHelper.Get<PlatformItem>(PlatformItem.Endpoint());
                    apiCallCount++; // increment API call count after making the call

                    if (apiResponse != null && apiResponse.response != null && apiResponse.response.systemes != null)
                    {
                        response = apiResponse.response.systemes;

                        // cache the response for future use
                        Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath) ?? string.Empty);
                        File.WriteAllText(cacheFilePath, Newtonsoft.Json.JsonConvert.SerializeObject(response));
                    }
                }
                catch (Exception ex)
                {
                    // log the error and return no match
                    Logging.Log(Logging.LogType.Critical, "ScreenScraper", $"Failed to fetch platforms from ScreenScraper API: {ex.Message}");
                    return new hasheous_server.Classes.DataObjects.MatchItem
                    {
                        MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                        MetadataId = ""
                    };
                }
            }

            // search the platform response for the provided platform name candidates and return a match if found
            foreach (var platform in response)
            {
                foreach (var candidate in SearchCandidates)
                {
                    // check the regional names
                    if (!string.IsNullOrEmpty(candidate) && platform.noms != null && platform.noms.Any(n => n.Value != null && n.Value.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                    {
                        return new hasheous_server.Classes.DataObjects.MatchItem
                        {
                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                            MetadataId = platform.id.ToString()
                        };
                    }

                    // check noms_commun, which is a comma separated list of common names for the platform
                    if (platform.noms.ContainsKey("noms_commun") && platform.noms["noms_commun"] != null)
                    {
                        var commonNames = platform.noms["noms_commun"].Split(',').Select(n => n.Trim());
                        if (commonNames.Any(n => n.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                        {
                            return new hasheous_server.Classes.DataObjects.MatchItem
                            {
                                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                MetadataId = platform.id.ToString()
                            };
                        }
                    }
                }
            }

            return new hasheous_server.Classes.DataObjects.MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };
        }

        /// <summary>
        /// Standard header class for ScreenScraper API results
        /// </summary>
        public class ssHeader
        {
            public string? APIversion { get; set; }
            public DateTime? dateTime { get; set; }
            public string? commandRequested { get; set; }
            public bool? success { get; set; }
            public string? error { get; set; }
        }

        /// <summary>
        /// Provides information about the ScreenScraper API servers
        /// </summary>
        public class ssServeurs
        {
            /// <summary>
            /// CPU usage of server 1 (average of the last 5 minutes)
            /// </summary>
            public string? cpu1 { get; set; }
            /// <summary>
            /// CPU usage of server 2 (average of the last 5 minutes)
            /// </summary>
            public string? cpu2 { get; set; }
            /// <summary>
            /// CPU usage of server 3 (average of the last 5 minutes)
            /// </summary>
            public string? cpu3 { get; set; }
            /// <summary>
            /// CPU usage of server 4 (average of the last 5 minutes)
            /// </summary>
            public string? cpu4 { get; set; }
            /// <summary>
            /// Number of accesses to the API since the last minute
            /// </summary>
            public string? threadsmin { get; set; }
            /// <summary>
            /// Number of scrapers using the api since the last minute
            /// </summary>
            public string? nbscrapeurs { get; set; }
            /// <summary>
            /// Number of accesses to the API in the current day (GMT+1)
            /// </summary>
            public string? apiacces { get; set; }
            /// <summary>
            /// Closed API for anonymous (unregistered or unidentified) (0: open / 1: closed)
            /// </summary>
            public string? closefornomember { get; set; }
            /// <summary>
            /// Closed API for non-participating members (no validated proposal) (0: open / 1: closed)
            /// </summary>
            public string? closeforleecher { get; set; }
            /// <summary>
            /// Maximum number of threads opened for anonymous (unregistered or unidentified) at the same time by the api
            /// </summary>
            public string? maxthreadfornonmember { get; set; }
            /// <summary>
            /// Current number of threads opened by anonymous (unregistered or unidentified) at the same time by the api
            /// </summary>
            public string? threadfornonmember { get; set; }
            /// <summary>
            /// Maximum number of threads open for members at the same time by the api
            /// </summary>
            public string? maxthreadformember { get; set; }
            /// <summary>
            /// Current number of threads opened by members at the same time by the api
            /// </summary>
            public string? threadformember { get; set; }
        }

        /// <summary>
        /// Provides information about the ScreenScraper API user, including API usage and rate limit information. This class is used to track how many API calls have been made and when the limits will reset to manage API rate limits effectively. 
        /// </summary>
        public class ssUser
        {
            /// <summary>
            /// username of the user on ScreenScraper
            /// </summary>
            public string? id { get; set; }
            /// <summary>
            /// user's digital identifier on ScreenScraper
            /// </summary>
            public string? numid { get; set; }
            /// <summary>
            /// user level on ScreenScraper
            /// </summary>
            public string? niveau { get; set; }
            /// <summary>
            /// level of financial contribution on ScreenScraper (2 = 1 Additional Thread / 3 and + = 5 Additional Threads)
            /// </summary>
            public string? contribution { get; set; }
            /// <summary>
            /// Counter of valid contributions (system media) proposed by the user
            /// </summary>
            public string? uploadsysteme { get; set; }
            /// <summary>
            /// Valid contribution counter (text info) proposed by the user
            /// </summary>
            public string? uploadinfos { get; set; }
            /// <summary>
            /// Valid contributions counter (association of roms) proposed by the user
            /// </summary>
            public string? romasso { get; set; }
            /// <summary>
            /// Counter of valid contributions (game media) proposed by the user
            /// </summary>
            public string? uploadmedia { get; set; }
            /// <summary>
            /// Number of user proposals validated by a moderator
            /// </summary>
            public string? propositionok { get; set; }
            /// <summary>
            /// Number of user proposals rejected by a moderator
            /// </summary>
            public string? propositionko { get; set; }
            /// <summary>
            /// Percentage of refusal of the user's proposal
            /// </summary>
            public string? quotarefu { get; set; }
            /// <summary>
            /// Number of threads allowed for the user (also indicated for non-registered)
            /// </summary>
            public string? maxthreads { get; set; }
            /// <summary>
            /// Download speed (in KB/s) allowed for the user (also indicated for non-registered)
            /// </summary>
            public string? maxdownloadspeed { get; set; }
            /// <summary>
            /// Total number of calls to the api during the day in short GMT+1 (resets at 0:00 GMT+1)
            /// </summary>
            public string? requeststoday { get; set; }
            /// <summary>
            /// Number of calls to the api with negative feedback (rom/game not found) during the day in short GMT+1 (resets at 0:00 GMT+1)
            /// </summary>
            public string? requestskotoday { get; set; }
            /// <summary>
            /// Maximum number of API calls allowed per minute for the user
            /// </summary>
            public string? maxrequestspermin { get; set; }
            /// <summary>
            /// Maximum number of calls to the API allowed per day for the user
            /// </summary>
            public string? maxrequestsperday { get; set; }
            /// <summary>
            /// Number of calls to the api with a negative feedback (rom/game not found) maximum allowed per day for the user
            /// </summary>
            public string? maxrequestskoperday { get; set; }
            /// <summary>
            /// number of user visits to ScreenScraper
            /// </summary>
            public string? visites { get; set; }
            /// <summary>
            /// date of the user's last visit to ScreenScraper (format: yyyy-mm-dd hh:mm:ss)
            /// </summary>
            public string? datedernierevisite { get; set; }
            /// <summary>
            /// favorite region of user visits on ScreenScraper (france,europe,usa,japon)
            /// </summary>
            public string? favregion { get; set; }
        }

        /// <summary>
        /// Represents a regional text item for the ScreenScraper API, containing information about the region and the associated text. This class is used to deserialize regional text data from the ScreenScraper API responses, allowing for structured access to localized information based on different regions. The region property indicates the specific region (e.g., France, Europe, USA, Japan) associated with the text, while the text property contains the localized information relevant to that region.
        /// </summary>
        public class ssRegionalText
        {
            /// <summary>
            /// Region associated with the text, such as France, Europe, USA, or Japan. This property is used to identify the specific region for which the text information is relevant, allowing for localized metadata retrieval based on regional preferences and differences in game releases or information.
            /// </summary>
            public string? region { get; set; }
            /// <summary>
            /// Text associated with the region, containing localized information relevant to that region.
            /// </summary>
            public string? text { get; set; }
        }

        /// <summary>
        /// Represents a text item for the ScreenScraper API, containing an identifier and the associated text. This class is used to deserialize text data from the ScreenScraper API responses, allowing for structured access to various pieces of information based on their identifiers. The id property serves as a unique identifier for the specific piece of information, while the text property contains the actual information or description associated with that identifier. This structure allows for flexible handling of different types of text information returned by the ScreenScraper API,
        /// </summary>
        public class ssTextId
        {
            /// <summary>
            /// Identifier for the text item, which can be used to categorize or reference specific pieces of information returned by the ScreenScraper API. This identifier allows for structured access to different types of text information, enabling the application to handle various metadata fields effectively based on their unique IDs.
            /// </summary>
            public string? id { get; set; }
            /// <summary>
            /// Text associated with the identifier, containing the actual information or description relevant to that identifier.
            /// </summary>
            public string? text { get; set; }
        }

        /// <summary>
        /// Represents a language-specific text item for the ScreenScraper API, containing the language code and the associated text. This class is used to deserialize language-specific text data from the ScreenScraper API responses, allowing for structured access to localized information based on different languages. The langue property indicates the specific language (e.g., "en" for English, "fr" for French) associated with the text, while the text property contains the localized information relevant to that language. This structure enables the application to handle multilingual metadata effectively based on the language preferences of users or regional differences in game information.
        /// </summary>
        public class ssLanguageText
        {
            /// <summary>
            /// Language code associated with the text, such as "en" for English or "fr" for French. This property is used to identify the specific language for which the text information is relevant, allowing for localized metadata retrieval based on language preferences and differences in game releases or information across different languages.
            /// </summary>
            public string? langue { get; set; }
            /// <summary>
            /// Text associated with the language code, containing the localized information relevant to that language.
            /// </summary>
            public string? text { get; set; }
        }

        /// <summary>
        /// Represents a game classification item for the ScreenScraper API, containing the type of classification and the associated text. This class is used to deserialize game classification data from the ScreenScraper API responses, allowing for structured access to different classifications or categories associated with a game. The type property indicates the specific type of classification (e.g., genre, theme, etc.), while the text property contains the information or description relevant to that classification type. This structure enables the application to handle various classifications of games effectively based on the information returned by the ScreenScraper API.
        /// </summary>
        public class ssGameClassification
        {
            /// <summary>
            /// Type of classification for the game, such as genre, theme, or other categories used by the ScreenScraper API to classify games. This property allows for structured access to different classifications associated with a game, enabling the application to organize and present metadata based on these classifications effectively.
            /// </summary>
            public string? type { get; set; }
            /// <summary>
            /// Text associated with the classification type, containing the information or description relevant to that classification.
            /// </summary>
            public string? text { get; set; }
        }

        /// <summary>
        /// Represents a game item for the ScreenScraper API, containing various properties such as ID, ROM ID, names in different regions, and other metadata fields. This class is used to deserialize game data from the ScreenScraper API responses, allowing for structured access to detailed information about games based on their ROM hashes or IDs. The properties include identifiers, names in different regions, developer and publisher information, player counts, ratings, and classifications, providing a comprehensive representation of a game as returned by the ScreenScraper API.
        /// </summary>
        public class ssGameDate
        {
            /// <summary>
            /// Region associated with the release date, such as France, Europe, USA, or Japan. This property is used to identify the specific region for which the release date information is relevant, allowing for localized metadata retrieval based on regional differences in game release dates.
            /// </summary>
            public string? region { get; set; }
            /// <summary>
            /// Release date of the game for the associated region, providing information about when the game was released in that specific region. This property allows for structured access to release date information based on regional differences, enabling the application to present accurate metadata about game releases across different regions as returned by the ScreenScraper API.
            /// </summary>
            public string? date { get; set; }
        }

        /// <summary>
        /// Represents a game genre item for the ScreenScraper API, containing properties such as ID, name, and parent-child relationships between genres. This class is used to deserialize game genre data from the ScreenScraper API responses, allowing for structured access to genre information associated with games. The properties include identifiers, names in different languages, and relationships between genres, providing a comprehensive representation of game genres as returned by the ScreenScraper API.
        /// </summary>
        public class ssGameGenre
        {
            /// <summary>
            /// ID of the genre, serving as a unique identifier for the genre in the ScreenScraper API. This property allows for structured access to genre information based on its unique ID, enabling the application to reference and organize genres effectively based on the data returned by the ScreenScraper API.
            /// </summary>
            public string? id { get; set; }
            /// <summary>
            /// Short name of the genre, providing a concise identifier for the genre. This property is used to access a brief name for the genre, which can be useful for display purposes or when referencing genres in a more compact form based on the data returned by the ScreenScraper API.
            /// </summary>
            public string? nomcourt { get; set; }
            /// <summary>
            /// Indicates whether this genre is the main genre for a game. This property can be used to identify the primary genre associated with a game, allowing for structured access to genre information based on its significance or relevance to the game as returned by the ScreenScraper API.
            /// </summary>
            public string? principale { get; set; }
            /// <summary>
            /// ID of the parent genre, if applicable, indicating a hierarchical relationship between genres. This property allows for structured access to genre information based on parent-child relationships, enabling the application to organize genres effectively based on their relationships as returned by the ScreenScraper API.
            /// </summary>
            public string? parentid { get; set; }
            /// <summary>
            /// List of names for the genre in different languages, providing localized information about the genre based on language preferences. This property allows for structured access to genre names in various languages, enabling the application to present genre information effectively based on the language preferences of users or regional differences in game information as returned by the ScreenScraper API.
            /// </summary>
            public List<ssLanguageText>? noms { get; set; }
        }

        /// <summary>
        /// Represents a game mode item for the ScreenScraper API, containing properties such as ID, name, and parent-child relationships between game modes. This class is used to deserialize game mode data from the ScreenScraper API responses, allowing for structured access to game mode information associated with games. The properties include identifiers, names in different languages, and relationships between game modes, providing a comprehensive representation of game modes as returned by the ScreenScraper API.
        /// </summary>
        public class ssGameMode
        {
            /// <summary>
            /// ID of the game mode, serving as a unique identifier for the game mode in the ScreenScraper API. This property allows for structured access to game mode information based on its unique ID, enabling the application to reference and organize game modes effectively based on the data returned by the ScreenScraper API.
            /// </summary>
            public string? id { get; set; }
            /// <summary>
            /// Short name of the game mode, providing a concise identifier for the game mode. This property is used to access a brief name for the game mode, which can be useful for display purposes or when referencing game modes in a more compact form based on the data returned by the ScreenScraper API.
            /// </summary>
            public string? nomcourt { get; set; }
            /// <summary>
            /// Indicates whether this game mode is the main mode for a game. This property can be used to identify the primary game mode associated with a game, allowing for structured access to game mode information based on its significance or relevance to the game as returned by the ScreenScraper API.
            /// </summary>
            public string? principale { get; set; }
            /// <summary>
            /// ID of the parent game mode, if applicable, indicating a hierarchical relationship between game modes. This property allows for structured access to game mode information based on parent-child relationships, enabling the application to organize game modes effectively based on their relationships as returned by the ScreenScraper API.
            /// </summary>
            public string? parentid { get; set; }
            /// <summary>
            /// List of names for the game mode in different languages, providing localized information about the game mode based on language preferences. This property allows for structured access to game mode names in various languages, enabling the application to present game mode information effectively based on the language preferences of users or regional differences in game information as returned by the ScreenScraper API.
            /// </summary>
            public List<ssLanguageText>? noms { get; set; }
        }

        /// <summary>
        /// Represents a game franchise item for the ScreenScraper API, containing properties such as ID, name, and parent-child relationships between franchises. This class is used to deserialize game franchise data from the ScreenScraper API responses, allowing for structured access to game franchise information associated with games. The properties include identifiers, names in different languages, and relationships between franchises, providing a comprehensive representation of game franchises as returned by the ScreenScraper API.
        /// </summary>
        public class ssGameFranchise
        {
            /// <summary>
            /// ID of the franchise, serving as a unique identifier for the franchise in the ScreenScraper API. This property allows for structured access to franchise information based on its unique ID, enabling the application to reference and organize franchises effectively based on the data returned by the ScreenScraper API.
            /// </summary>
            public string? id { get; set; }
            /// <summary>
            /// Short name of the franchise, providing a concise identifier for the franchise. This property is used to access a brief name for the franchise, which can be useful for display purposes or when referencing franchises in a more compact form based on the data returned by the ScreenScraper API.
            /// </summary>
            public string? nomcourt { get; set; }
            /// <summary>
            /// Indicates whether this franchise is the main franchise for a game. This property can be used to identify the primary franchise associated with a game, allowing for structured access to franchise information based on its significance or relevance to the game as returned by the ScreenScraper API.
            /// </summary>
            public string? principale { get; set; }
            /// <summary>
            /// ID of the parent franchise, if applicable, indicating a hierarchical relationship between franchises. This property allows for structured access to franchise information based on parent-child relationships, enabling the application to organize franchises effectively based on their relationships as returned by the ScreenScraper API.
            /// </summary>
            public string? parentid { get; set; }
            /// <summary>
            /// List of names for the franchise in different languages, providing localized representations of the franchise name. This property allows for structured access to franchise names based on language, enabling the application to display franchise names appropriately for different locales as returned by the ScreenScraper API.
            /// </summary>
            public List<ssLanguageText>? noms { get; set; }
        }

        /// <summary>
        /// Represents a media item for the ScreenScraper API, containing properties such as type, URL, region, and various hash values. This class is used to deserialize media data from the ScreenScraper API responses, allowing for structured access to media information. The properties include the type of media (e.g., screenshot, box art), the URL where the media can be accessed, the region associated with the media, and various hash values (CRC, MD5, SHA1) for verifying the integrity of the media file. This structure provides a comprehensive representation of media as returned by the ScreenScraper API.
        /// </summary>
        public class ssMedia
        {
            /// <summary>
            /// Type of media, such as "screenshot", "boxart", "banner", etc., indicating the category or purpose of the media item. This property allows for structured access to media information based on its type, enabling the application to organize and present media effectively based on the type of media returned by the ScreenScraper API.
            /// </summary>
            public string? type { get; set; }
            /// <summary>
            /// URL where the media can be accessed, providing a direct link to the media file associated with the game. This property allows for structured access to media information based on its URL, enabling the application to retrieve and display media effectively based on the URL provided by the ScreenScraper API.
            /// </summary>
            public string? parent { get; set; }
            /// <summary>
            /// URL where the media can be accessed, providing a direct link to the media file associated with the game. This property allows for structured access to media information based on its URL, enabling the application to retrieve and display media effectively based on the URL provided by the ScreenScraper API.
            /// </summary>
            public string? url { get; set; }
            /// <summary>
            /// Region associated with the media, such as France, Europe, USA, or Japan. This property is used to identify the specific region for which the media information is relevant, allowing for localized metadata retrieval based on regional differences in game releases or information as returned by the ScreenScraper API.
            /// </summary>
            public string? region { get; set; }
            /// <summary>
            /// Indicates whether the media is the main media for the game (0 = no / 1 = yes). This property can be used to identify the primary media associated with a game, allowing for structured access to media information based on its significance or relevance to the game as returned by the ScreenScraper API.
            /// </summary>
            public string? support { get; set; }
            /// <summary>
            /// CRC hash value for the media file, used for verifying the integrity of the media file. This property allows for structured access to media information based on its CRC hash, enabling the application to validate the media file effectively based on the hash value provided by the ScreenScraper API.
            /// </summary>
            public string? crc { get; set; }
            /// <summary>
            /// MD5 hash value for the media file, used for verifying the integrity of the media file. This property allows for structured access to media information based on its MD5 hash, enabling the application to validate the media file effectively based on the hash value provided by the ScreenScraper API.
            /// </summary>
            public string? md5 { get; set; }
            /// <summary>
            /// SHA1 hash value for the media file, used for verifying the integrity of the media file. This property allows for structured access to media information based on its SHA1 hash, enabling the application to validate the media file effectively based on the hash value provided by the ScreenScraper API.
            /// </summary>
            public string? sha1 { get; set; }
            /// <summary>
            /// Size of the media file, providing information about the file's storage requirements. This property allows for structured access to media information based on its size, enabling the application to manage storage and display media effectively based on the size information provided by the ScreenScraper API.
            /// </summary>
            public string? size { get; set; }
            /// <summary>
            /// Format of the media file, indicating the file type or encoding used. This property allows for structured access to media information based on its format, enabling the application to handle and display media effectively based on the format information provided by the ScreenScraper API.
            /// </summary>
            public string? format { get; set; }
        }

        public class ssRom
        {
            /// <summary>
            /// numeric identifier of the rom
            /// </summary>
            public long? Id { get; set; }
            /// <summary>
            /// support number (ex: 1 = floppy disk 01 or CD 01)
            /// </summary>
            public int? Romnumsupport { get; set; }
            /// <summary>
            /// total number of supports (ex: 2 = 2 floppy disks or 2 CDs)
            /// </summary>
            public int? romtotalsupport { get; set; }
            /// <summary>
            /// name of the rom file or folder
            /// </summary>
            public string? romfilename { get; set; }
            /// <summary>
            /// octect size of the rom file or size of the contents of the folder
            /// </summary>
            public int? romsize { get; set; }
            /// <summary>
            /// result of the CRC32 calculation of the rom file or the largest file of the "rom" folder
            /// </summary>
            public string? romcrc { get; set; }
            /// <summary>
            /// result of the MD5 calculation of the rom file or the largest file of the "rom" folder
            /// </summary>
            public string? rommd5 { get; set; }
            /// <summary>
            /// result of the SHA1 calculation of the rom file or the largest file of the "rom" folder
            /// </summary>
            public string? romsha1 { get; set; }
            /// <summary>
            /// digital identifier of the parent rom if the rom is a clone (Arcade Systems)
            /// </summary>
            public long? romcloneof { get; set; }
            /// <summary>
            /// Beta version of the game (0 = no / 1 = yes)
            /// </summary>
            public int? Beta { get; set; }
            /// <summary>
            /// Demo version of the game (0 = no / 1 = yes)
            /// </summary>
            public int? Demo { get; set; }
            /// <summary>
            /// Translated version of the game (0 = no / 1 = yes)
            /// </summary>
            public int? trad { get; set; }
            /// <summary>
            /// Modified version of the game (0 = no / 1 = yes)
            /// </summary>
            public int? hack { get; set; }
            /// <summary>
            /// Game not "Official" (0 = no / 1 = yes)
            /// </summary>
            public int? Unl { get; set; }
            /// <summary>
            /// Alternative version of the game (0 = no / 1 = yes)
            /// </summary>
            public int? alt { get; set; } = 0;
            /// <summary>
            /// Best version of the game (0 = no / 1 = yes)
            /// </summary>
            public int? best { get; set; }
            /// <summary>
            /// Compatible Retro Achievement (0 = no / 1 = yes)
            /// </summary>
            public int? Retroachievement { get; set; }
            /// <summary>
            /// Gamelink compatible (0 = no / 1 = yes)
            /// </summary>
            public int? Gamelink { get; set; }
            /// <summary>
            /// Total number of times scraped
            /// </summary>
            public int? nbscrap { get; set; }
            /// <summary>
            /// List of supported languages
            /// </summary>
            public Dictionary<string, List<string>>? langues { get; set; }
            /// <summary>
            /// List of supported regions
            /// </summary>
            public Dictionary<string, List<string>>? regions { get; set; }
        }

        /// <summary>
        /// Represents a game item for the ScreenScraper API, containing various properties such as ID, ROM ID, names in different regions, and other metadata fields. This class is used to deserialize game data from the ScreenScraper API responses, allowing for structured access to detailed information about games based on their ROM hashes or IDs. The properties include identifiers, names in different regions, developer and publisher information, player counts, ratings, classifications, release dates, genres, modes, franchises, and associated media, providing a comprehensive representation of a game as returned by the ScreenScraper API.
        /// </summary>
        public class ssGame
        {
            /// <summary>
            /// ID of the game, serving as a unique identifier for the game in the ScreenScraper API. This property allows for structured access to game information based on its unique ID, enabling the application to reference and organize games effectively based on the data returned by the ScreenScraper API.
            /// </summary>
            public long? id { get; set; }
            /// <summary>
            /// ROM ID associated with the game, providing a reference to the specific ROM for which the metadata is relevant. This property allows for structured access to game information based on its ROM ID, enabling the application to manage and present metadata effectively based on the ROM information provided by the ScreenScraper API.
            /// </summary>
            public long? romid { get; set; }
            /// <summary>
            /// Indicates whether the item is not a game, which can be used to filter out non-game items from the metadata results. This property allows for structured access to game information based on its classification as a game or non-game item, enabling the application to manage and present metadata effectively based on the type of item returned by the ScreenScraper API.
            /// </summary>
            public bool? notgame { get; set; }
            /// <summary>
            /// List of names for the game in different regions, providing localized information about the game's title based on regional preferences. This property allows for structured access to game names in various regions, enabling the application to present game information effectively based on regional differences in game titles as returned by the ScreenScraper API.
            /// </summary>
            public List<ssRegionalText>? noms { get; set; }
            /// <summary>
            /// Indicates if the game is a clone of another game, which can be used to identify and manage metadata for games that are variations or derivatives of other games. This property allows for structured access to game information based on its classification as a clone, enabling the application to handle and present metadata effectively based on the relationships between games as returned by the ScreenScraper API.
            /// </summary>
            public string? cloneof { get; set; }
            /// <summary>
            /// System associated with the game, providing information about the platform or console for which the game was released. This property allows for structured access to game information based on its associated system, enabling the application to manage and present metadata effectively based on the platform information provided by the ScreenScraper API.
            /// </summary>
            public ssTextId? systeme { get; set; }
            /// <summary>
            /// Publisher of the game, providing information about the company or entity responsible for publishing the game. This property allows for structured access to game information based on its publisher, enabling the application to manage and present metadata effectively based on the publisher information provided by the ScreenScraper API.
            /// </summary>
            public ssTextId? editeur { get; set; }
            /// <summary>
            /// Developer of the game, providing information about the company or entity responsible for developing the game. This property allows for structured access to game information based on its developer, enabling the application to manage and present metadata effectively based on the developer information provided by the ScreenScraper API.
            /// </summary>
            public ssTextId? developpeur { get; set; }
            /// <summary>
            /// Number of players supported by the game, providing information about the multiplayer capabilities of the game. This property allows for structured access to game information based on its player count, enabling the application to manage and present metadata effectively based on the multiplayer information provided by the ScreenScraper API.
            /// </summary>
            public KeyValuePair<string, string>? joueurs { get; set; }
            /// <summary>
            /// Rating of the game, providing information about the game's quality or popularity based on user ratings or reviews. This property allows for structured access to game information based on its rating, enabling the application to manage and present metadata effectively based on the rating information provided by the ScreenScraper API.
            /// </summary>
            public KeyValuePair<string, string>? note { get; set; }
            /// <summary>
            /// Top staff associated with the game, providing information about key personnel involved in the game's development or production. This property allows for structured access to game information based on its top staff, enabling the application to manage and present metadata effectively based on the personnel information provided by the ScreenScraper API.
            /// </summary>
            public string? topstaff { get; set; }
            /// <summary>
            /// Rotation of the game, providing information about the orientation or display settings for the game. This property allows for structured access to game information based on its rotation, enabling the application to manage and present metadata effectively based on the display information provided by the ScreenScraper API.
            /// </summary>
            public string? rotation { get; set; }
            /// <summary>
            /// Synopsis of the game, providing a brief description or summary of the game's plot, gameplay, or features. This property allows for structured access to game information based on its synopsis, enabling the application to manage and present metadata effectively based on the descriptive information provided by the ScreenScraper API.
            /// </summary>
            public List<ssLanguageText>? synopsis { get; set; }
            /// <summary>
            /// List of classifications associated with the game, providing information about the various categories or classifications that apply to the game. This property allows for structured access to game information based on its classifications, enabling the application to manage and present metadata effectively based on the classification information provided by the ScreenScraper API.
            /// </summary>
            public List<ssGameClassification>? classifications { get; set; }
            /// <summary>
            /// List of release dates for the game in different regions, providing information about when the game was released in various regions. This property allows for structured access to game information based on its release dates, enabling the application to manage and present metadata effectively based on regional differences in game release dates as returned by the ScreenScraper API.
            /// </summary>
            public List<ssGameDate>? dates { get; set; }
            /// <summary>
            /// List of genres associated with the game, providing information about the various genres that apply to the game. This property allows for structured access to game information based on its genres, enabling the application to manage and present metadata effectively based on the genre information provided by the ScreenScraper API.
            /// </summary>
            public List<ssGameGenre>? genres { get; set; }
            /// <summary>
            /// List of game modes associated with the game, providing information about the various modes of play that apply to the game. This property allows for structured access to game information based on its game modes, enabling the application to manage and present metadata effectively based on the game mode information provided by the ScreenScraper API.
            /// </summary>
            public List<ssGameMode>? modes { get; set; }
            /// <summary>
            /// List of franchises associated with the game, providing information about the various franchises that apply to the game. This property allows for structured access to game information based on its franchises, enabling the application to manage and present metadata effectively based on the franchise information provided by the ScreenScraper API.
            /// </summary>
            public List<ssGameFranchise>? familles { get; set; }
            /// <summary>
            /// List of media associated with the game, providing information about the various media types that apply to the game. This property allows for structured access to game information based on its media, enabling the application to manage and present metadata effectively based on the media information provided by the ScreenScraper API.
            /// </summary>
            public List<ssMedia>? medias { get; set; }
            /// <summary>
            /// List of ROMs associated with the game, providing information about the various ROM files that apply to the game. This property allows for structured access to game information based on its ROMs, enabling the application to manage and present metadata effectively based on the ROM information provided by the ScreenScraper API.
            /// </summary>
            public List<ssRom>? roms { get; set; }
        }

        /// <summary>
        /// Represents a platform item for the ScreenScraper API, containing properties such as ID, parent ID, and names in different languages. This class is used to deserialize platform data from the ScreenScraper API responses, allowing for structured access to platform information associated with games. The properties include identifiers, parent-child relationships between platforms, and names in different languages, providing a comprehensive representation of platforms as returned by the ScreenScraper API.
        /// </summary>
        public class ssPlatform
        {
            /// <summary>
            /// digital identifier of the system (to be provided again in other API requests)
            /// </summary>
            public long? id { get; set; }
            /// <summary>
            /// digital identifier of the parent system
            /// </summary>
            public long? parentid { get; set; }
            /// List of names for the platform in different languages, providing localized information about the platform based on language preferences. This property allows for structured access to platform names in various languages, enabling the application to present platform information effectively based on the language preferences of users or regional differences in platform information as returned by the ScreenScraper API.
            /// </summary>
            public Dictionary<string, string> noms { get; set; }
            /// <summary>
            /// extensions of usable rom files (all emulators combined)
            /// </summary>
            public string? extensions { get; set; }
            /// <summary>
            /// Name of the system production company
            /// </summary>
            public string? compagnie { get; set; }
            /// <summary>
            /// System type (Arcade,Console,Portable Console,Arcade Emulation,Fipper,Online,Computer,Smartphone)
            /// </summary>
            public string? type { get; set; }
            /// <summary>
            /// Year of production start
            /// </summary>
            public string? datedebut { get; set; }
            /// <summary>
            /// Year of end of production
            /// </summary>
            public string? datefin { get; set; }
            /// <summary>
            /// Type(s) of roms
            /// </summary>
            public string? romtype { get; set; }
            /// <summary>
            /// Type of the original system media
            /// </summary>
            public string? romTypesListe { get; set; }
            /// <summary>
            /// List of media associated with the platform
            /// </summary>
            public List<ssMedia>? medias { get; set; }
        }

        /// <summary>
        /// Maps to the ssuser ScreenScraper API object, used to guage how many API calls are being made and to manage the API rate limits by tracking the number of calls made and the time until the next reset.
        /// </summary>
        public class UserItem
        {
            /// <summary>
            /// Gets the ScreenScraper API endpoint URL for retrieving user information, including the client ID and secret from the configuration settings. This endpoint is used to check the API rate limits and usage for the ScreenScraper API.
            /// </summary>
            public static string Endpoint()
            {
                return $"https://api.screenscraper.fr/api2/ssuserInfos.php?devid={Config.ScreenScraperConfiguration.DevClientId}&devpassword={Config.ScreenScraperConfiguration.DevSecret}&softname=Hasheous&output=json&ssid={Config.ScreenScraperConfiguration.ClientId}&sspassword={Config.ScreenScraperConfiguration.Secret}";
            }

            /// <summary>
            /// Standard header for ScreenScraper API responses, containing information about the API version, request time, command requested, success status, and any error messages. This header is included in the user information response to provide context about the API request and response.
            /// </summary>
            public ssHeader? header { get; set; }

            /// <summary>
            /// Contains information about the ScreenScraper API servers and the user, including API usage and rate limit information. This information is used to manage API rate limits effectively by tracking how many calls have been made and when the limits will reset. The server information provides insights into the current load on the API servers, while the user information tracks the API usage for the specific user account, allowing the application to avoid exceeding the limits and ensure smooth operation.
            /// </summary>
            public UserInfoResponse? response { get; set; }

            /// <summary>
            /// Represents the response from the ScreenScraper API when fetching user information, including server status and user API usage details. This class is used to deserialize the JSON response from the API and provides structured access to the server and user information needed to manage API rate limits effectively.
            /// </summary>
            public class UserInfoResponse
            {
                /// <summary>
                /// Information about the ScreenScraper API servers, including CPU usage, thread counts, and API access details. This information helps gauge the current load on the API servers and can be used to make informed decisions about when to make API calls to avoid overloading the servers.
                /// </summary>
                public ssServeurs? serveurs { get; set; }
                /// <summary>
                /// Information about the ScreenScraper API user, including API usage and rate limit details. This information is crucial for managing API rate limits effectively by tracking how many calls have been made and when the limits will reset, allowing the application to avoid exceeding the limits and ensure smooth operation.
                /// </summary>
                public ssUser? ssuser { get; set; }
            }
        }

        /// <summary>
        /// Represents a game item for the ScreenScraper API, providing a method to construct the API endpoint URL for retrieving game information based on either the game ID or ROM hashes (MD5 and SHA1). This class is used to generate the correct endpoint for fetching game metadata from the ScreenScraper API, allowing for flexible searching by either ID or hash values. The method ensures that the necessary parameters are provided and constructs the appropriate URL for the API request.
        /// </summary>
        public class GameItem
        {
            /// <summary>
            /// Constructs the ScreenScraper API endpoint URL for retrieving game information based on the provided ID or ROM hashes (MD5 and SHA1). If an ID is provided, it constructs the endpoint using the ID. If no ID is provided, it requires both MD5 and SHA1 hashes to construct the endpoint for searching by hash. This method ensures that the correct endpoint is generated based on the available information for retrieving game metadata from the ScreenScraper API.
            /// </summary>
            /// <param name="id">The ID of the game to retrieve information for.</param>
            /// <param name="md5hash">The MD5 hash of the game's ROM.</param>
            /// <param name="sha1hash">The SHA1 hash of the game's ROM.</param>
            /// <returns>The constructed ScreenScraper API endpoint URL.</returns>
            /// <exception cref="ArgumentException">Thrown when neither ID nor valid hashes are provided.</exception>
            public static string Endpoint(long? id = null, string? md5hash = null, string? sha1hash = null)
            {
                // if we have an ID, we can construct the endpoint directly
                if (id.HasValue)
                {
                    return $"https://api.screenscraper.fr/api2/jeuInfos.php?devid={Config.ScreenScraperConfiguration.DevClientId}&devpassword={Config.ScreenScraperConfiguration.DevSecret}&softname=Hasheous&output=json&ssid={Config.ScreenScraperConfiguration.ClientId}&sspassword={Config.ScreenScraperConfiguration.Secret}&id={id.Value}";
                }

                // if we don't have an ID, we need to search by hash, either MD5 or SHA1 (or both) must be provided
                if (String.IsNullOrEmpty(md5hash) && String.IsNullOrEmpty(sha1hash))
                {
                    throw new ArgumentException("Both MD5 and SHA1 hashes must be provided to construct the ScreenScraper game endpoint.");
                }
                else if (!String.IsNullOrEmpty(md5hash))
                {
                    return $"https://api.screenscraper.fr/api2/jeuInfos.php?devid={Config.ScreenScraperConfiguration.DevClientId}&devpassword={Config.ScreenScraperConfiguration.DevSecret}&softname=Hasheous&output=json&ssid={Config.ScreenScraperConfiguration.ClientId}&sspassword={Config.ScreenScraperConfiguration.Secret}&md5={md5hash}";
                }
                else if (!String.IsNullOrEmpty(sha1hash))
                {
                    return $"https://api.screenscraper.fr/api2/jeuInfos.php?devid={Config.ScreenScraperConfiguration.DevClientId}&devpassword={Config.ScreenScraperConfiguration.DevSecret}&softname=Hasheous&output=json&ssid={Config.ScreenScraperConfiguration.ClientId}&sspassword={Config.ScreenScraperConfiguration.Secret}&sha1={sha1hash}";
                }
                else
                {
                    throw new ArgumentException("At least one of MD5 or SHA1 hash must be provided to construct the ScreenScraper game endpoint.");
                }
            }

            /// <summary>
            /// Standard header for ScreenScraper API responses, containing information about the API version, request time, command requested, success status, and any error messages. This header is included in the user information response to provide context about the API request and response.
            /// </summary>
            public ssHeader? header { get; set; }

            /// <summary>
            /// Contains information about the ScreenScraper API servers and the user, including API usage and rate limit information, as well as detailed information about the game retrieved from the API. This information is used to manage API rate limits effectively by tracking how many calls have been made and when the limits will reset, while also providing structured access to comprehensive game metadata based on the data returned by the ScreenScraper API. The server information provides insights into the current load on the API servers, while the user information tracks the API usage for the specific user account, allowing the application to avoid exceeding the limits and ensure smooth operation. The game information includes various metadata fields such as names, developer, publisher, player counts, ratings, classifications, release dates, genres, modes, franchises, and associated media, enabling the application to manage and present metadata effectively based on the detailed information provided for each game.
            /// </summary>
            public GameInfoResponse? response { get; set; }

            /// <summary>
            /// Represents the response from the ScreenScraper API when fetching game information, including server status, user API usage details, and comprehensive game metadata. This class is used to deserialize the JSON response from the API and provides structured access to the server, user, and game information needed to manage API rate limits effectively while also providing detailed metadata about the game as returned by the ScreenScraper API.
            /// </summary>
            public class GameInfoResponse
            {
                /// <summary>
                /// Information about the ScreenScraper API servers, including CPU usage, thread counts, and API access details. This information helps gauge the current load on the API servers and can be used to make informed decisions about when to make API calls to avoid overloading the servers.
                /// </summary>
                public ssServeurs? serveurs { get; set; }
                /// <summary>
                /// Information about the ScreenScraper API user, including API usage and rate limit details. This information is crucial for managing API rate limits effectively by tracking how many calls have been made and when the limits will reset, allowing the application to avoid exceeding the limits and ensure smooth operation.
                /// </summary>
                public ssUser? ssuser { get; set; }
                /// <summary>
                /// Detailed information about the game retrieved from the ScreenScraper API, including various metadata fields such as names, developer, publisher, player counts, ratings, classifications, release dates, genres, modes, franchises, and associated media. This property allows for structured access to comprehensive game information based on the data returned by the ScreenScraper API, enabling the application to manage and present metadata effectively based on the detailed information provided for each game.
                /// </summary>
                public ssGame? jeu { get; set; }
            }
        }

        public class PlatformItem
        {
            /// <summary>
            /// Gets the ScreenScraper API endpoint URL for retrieving platform information, which returns a list of all platforms available in the ScreenScraper database. Since the ScreenScraper API does not support server-side filtering for platforms, this endpoint retrieves all platforms, and any necessary filtering must be done client-side based on the data returned by the API. This method constructs the endpoint URL using the client ID and secret from the configuration settings, allowing for authenticated access to the ScreenScraper API to fetch platform metadata.
            /// </summary>
            public static string Endpoint()
            {
                // ScreenScraper's endpoint for platform metadata returns ALL platforms with no server side filtering
                return $"https://api.screenscraper.fr/api2/systemesListe.php?devid=devid={Config.ScreenScraperConfiguration.DevClientId}&devpassword={Config.ScreenScraperConfiguration.DevSecret}&softname=Hasheous&output=json&ssid={Config.ScreenScraperConfiguration.ClientId}&sspassword={Config.ScreenScraperConfiguration.Secret}";
            }

            /// <summary>
            /// Standard header for ScreenScraper API responses, containing information about the API version, request time, command requested, success status, and any error messages. This header is included in the user information response to provide context about the API request and response.
            /// </summary>
            public ssHeader? header { get; set; }

            /// <summary>
            /// Contains information about the ScreenScraper API servers and the user, including API usage and rate limit information, as well as detailed information about the platforms retrieved from the API. This information is used to manage API rate limits effectively by tracking how many calls have been made and when the limits will reset, while also providing structured access to comprehensive platform metadata based on the data returned by the ScreenScraper API. The server information provides insights into the current load on the API servers, while the user information tracks the API usage for the specific user account, allowing the application to avoid exceeding the limits and ensure smooth operation. The platform information includes various metadata fields such as platform names, release dates, manufacturers, and other relevant details, enabling the application to manage and present metadata effectively based on the detailed information provided for each platform.
            /// </summary>
            public PlatformInfoResponse? response { get; set; }

            /// <summary>
            /// Represents the response from the ScreenScraper API when fetching platform information, including server status, user API usage details, and comprehensive platform metadata. This class is used to deserialize the JSON response from the API and provides structured access to the server, user, and platform information needed to manage API rate limits effectively while also providing detailed metadata about the platforms as returned by the ScreenScraper API.
            /// </summary>
            public class PlatformInfoResponse
            {
                /// <summary>
                /// Information about the ScreenScraper API servers, including CPU usage, thread counts, and API access details. This information helps gauge the current load on the API servers and can be used to make informed decisions about when to make API calls to avoid overloading the servers.
                /// </summary>
                public ssServeurs? serveurs { get; set; }

                /// <summary>
                /// Information about the ScreenScraper API user, including API usage and rate limit details. This information is crucial for managing API rate limits effectively by tracking how many calls have been made and when the limits will reset, allowing the application to avoid exceeding the limits and ensure smooth operation.
                /// </summary>
                public List<ssPlatform>? systemes { get; set; }
            }
        }
    }
}