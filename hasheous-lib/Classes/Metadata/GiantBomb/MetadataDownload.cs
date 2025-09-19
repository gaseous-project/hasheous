using System.Data;
using System.Xml;
using Classes;
using GiantBomb.Models;

namespace GiantBomb
{
    public class MetadataDownload
    {
        private static readonly HttpClient client = new HttpClient();
        private static bool headersConfigured = false;

        // --- Rate limiting fields ---
        // Updated policy:
        //   Soft threshold: 200 requests in rolling 1h. After this we dramatically slow down to ~1 request / 2-3 minutes.
        //   Hard threshold: 300 requests in rolling 1h. After this we revert to strict blocking until window frees (original behaviour).
        //   Below 200: light velocity limit (>=2s spacing) to avoid bursts.
        private static readonly SemaphoreSlim _rateGate = new SemaphoreSlim(1, 1); // serialize rate calculations
        private static readonly Queue<DateTime> _recentRequests = new Queue<DateTime>(); // timestamps of recent requests
        private static readonly TimeSpan _hourWindow = TimeSpan.FromHours(1);
        private static readonly int _softHourlyThreshold = 200; // start slowdown
        private static readonly int _hardHourlyMax = 300;        // absolute blocking cap
        private static readonly TimeSpan _minSpacing = TimeSpan.FromSeconds(2); // baseline spacing under soft threshold
        private static readonly Random _rng = new Random();

        // Central wait logic. Ensures we:
        // 1. Do not exceed 200 requests in any rolling 60 minute window.
        // 2. Space individual requests by at least _minSpacing to avoid short-term bursts.
        private static async Task WaitForRateLimitAsync(CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                TimeSpan? waitNeeded = null;
                bool softSlowdownApplied = false;
                await _rateGate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var now = DateTime.UtcNow;
                    // Drop expired timestamps (older than 1 hour)
                    while (_recentRequests.Count > 0 && (now - _recentRequests.Peek()) > _hourWindow)
                        _recentRequests.Dequeue();

                    var count = _recentRequests.Count;

                    if (count >= _hardHourlyMax)
                    {
                        // Hard cap: behave like original logic (strict block until a slot frees)
                        var oldest = _recentRequests.Peek();
                        var releaseAt = oldest + _hourWindow; // when that oldest request exits the 1h window
                        waitNeeded = releaseAt - now;
                    }
                    else if (count >= _softHourlyThreshold)
                    {
                        // Soft slowdown region (200-299): enforce large spacing 2-3 minutes between requests.
                        if (count > 0)
                        {
                            var last = _recentRequests.Last();
                            var sinceLast = now - last;
                            // Random per-attempt spacing between 2 and 3 minutes.
                            var targetSpacing = TimeSpan.FromMinutes(2 + _rng.NextDouble());
                            if (sinceLast < targetSpacing)
                            {
                                waitNeeded = targetSpacing - sinceLast;
                                softSlowdownApplied = true;
                            }
                        }
                    }
                    else if (count > 0)
                    {
                        // Below soft threshold: light velocity spacing (>= _minSpacing)
                        var last = _recentRequests.Last();
                        var sinceLast = now - last;
                        if (sinceLast < _minSpacing)
                            waitNeeded = _minSpacing - sinceLast;
                    }

                    if (waitNeeded == null || waitNeeded <= TimeSpan.Zero)
                    {
                        // Reserve a slot now (we count attempted calls whether they succeed or 420/429)
                        _recentRequests.Enqueue(now);
                        return; // proceed
                    }
                }
                finally
                {
                    _rateGate.Release();
                }

                if (waitNeeded.HasValue && waitNeeded.Value > TimeSpan.Zero)
                {
                    if (softSlowdownApplied)
                        Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Soft threshold reached (>= {_softHourlyThreshold}). Slowdown wait {waitNeeded.Value.TotalSeconds:F1}s. Current hour count={_recentRequests.Count}.");
                    else if (_recentRequests.Count >= _hardHourlyMax)
                        Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Hard threshold {_hardHourlyMax} reached. Blocking for {waitNeeded.Value.TotalSeconds:F1}s until window frees.");
                    else
                        Logging.Log(Logging.LogType.Information, "GiantBomb", $"Rate limiter sleeping {waitNeeded.Value.TotalSeconds:F1}s (velocity control).");
                    await Task.Delay(waitNeeded.Value, ct).ConfigureAwait(false);
                }
            }
        }

        // check if Config.GiantBomb.BaseURL is set, otherwise use default
        string BaseUrl = Config.GiantBomb.BaseURL ?? "https://www.giantbomb.com/";

        /// <summary>
        /// Root directory where GiantBomb metadata is cached locally.
        /// </summary>
        public string LocalFilePath
        {
            get
            {
                return Config.LibraryConfiguration.LibraryMetadataDirectory_GiantBomb;
            }
        }

        public static object? GetTracking(string key, object? defaultValue = null)
        {
            try
            {
                string dir = Config.LibraryConfiguration.LibraryMetadataDirectory_GiantBomb;
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                    return defaultValue;

                string trackingFile = Path.Combine(dir, "tracking.json");
                if (!File.Exists(trackingFile))
                    return defaultValue;

                string json = File.ReadAllText(trackingFile);
                if (string.IsNullOrWhiteSpace(json))
                    return defaultValue;

                Dictionary<string, object>? data;
                try
                {
                    data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                }
                catch
                {
                    return defaultValue;
                }

                if (data != null && data.TryGetValue(key, out var value) && value != null)
                    return value;

                return defaultValue;
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Error reading tracking key '{key}': {ex.Message}", ex);
                return defaultValue;
            }
        }

        public static void SetTracking(string key, object value)
        {
            try
            {
                string dir = Config.LibraryConfiguration.LibraryMetadataDirectory_GiantBomb;
                if (string.IsNullOrWhiteSpace(dir))
                    return;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string trackingFile = Path.Combine(dir, "tracking.json");
                Dictionary<string, object> data = new Dictionary<string, object>();
                if (File.Exists(trackingFile))
                {
                    string json = File.ReadAllText(trackingFile);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        try
                        {
                            var existingData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                            if (existingData != null)
                                data = existingData;
                        }
                        catch
                        {
                            // Ignore errors and start fresh
                        }
                    }
                }

                data[key] = value;
                string newJson = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(trackingFile, newJson);
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Error writing tracking key '{key}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Database/schema name used for GiantBomb metadata tables.
        /// </summary>
        public string dbName { get; set; } = "giantbomb";

        /// <summary>
        /// Age in days after which cached data will be refreshed.
        /// </summary>
        public int TimeToExpire { get; set; } = 30; // Default expiration time for cached data

        /// <summary>
        /// Last update date in "yyyy-MM-dd" format. Used for fetching incremental updates.
        /// </summary>
        public string LastUpdate
        {
            get
            {
                var lastUpdateObj = GetTracking("GiantBomb_LastUpdate", DateTime.Parse("1970-01-01 00:00:00"));
                DateTime lastUpdate = lastUpdateObj is DateTime dt ? dt : DateTime.Parse("1970-01-01 00:00:00");
                return lastUpdate.ToString("yyyy-MM-dd");
            }
        }

        /// <summary>
        /// GiantBomb requires an end date for it's date filter - set far in the future to ensure all updates are captured.
        /// </summary>
        public string UpdateEndDate { get; } = "3000-01-01";

        // Legacy per-minute limiter removed (MaximumRequestsPerMinute / RateLimitSleepTime / checkRequestLimit) – superseded by async sliding window limiter above.


        /// <summary>
        /// Downloads platform data from GiantBomb and updates the local database asynchronously.
        /// </summary>
        public async Task DownloadPlatforms()
        {
            // ensure that the local file path exists
            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            // setup database if it doesn't exist
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            // add indexes to the Platform table
            // Add an index to the Name column
            string indexQuery = $"CREATE INDEX IF NOT EXISTS `idx_Platform_name` ON `{dbName}`.`Platform` (`name`); CREATE INDEX IF NOT EXISTS `idx_Platform_aliases` ON `{dbName}`.`Platform` (`aliases`);";
            Console.WriteLine($"Executing query: {indexQuery}");
            await Task.Run(() => db.ExecuteNonQuery(indexQuery));

            // Add a Full Text Search index to the Name column
            // Check if the FULLTEXT index already exists before adding it
            string checkFullTextIndexQuery = $@"
                        SELECT * 
                        FROM information_schema.STATISTICS 
                        WHERE table_schema = '{dbName}' 
                          AND table_name = 'Platform' 
                          AND index_name = 'ft_idx_Platform_name'";
            var fullTextIndexExists = Convert.ToInt32((await db.ExecuteCMDAsync(checkFullTextIndexQuery)).Rows.Count) > 0;

            string fullTextIndexQuery = $"ALTER TABLE `{dbName}`.`Platform` ADD FULLTEXT INDEX `ft_idx_Platform_name` (`name`); ALTER TABLE `{dbName}`.`Platform` ADD FULLTEXT INDEX `ft_idx_Platform_aliases` (`aliases`);";
            if (!fullTextIndexExists)
            {
                Console.WriteLine($"Executing query: {fullTextIndexQuery}");
                await Task.Run(() => db.ExecuteNonQuery(fullTextIndexQuery));
            }
            else
            {
                Console.WriteLine("FullText index 'ft_idx_Game_name' already exists. Skipping creation.");
            }

            // get the last platform fetch date and time from settings
            var lastPlatformFetchObj = GetTracking("GiantBomb_LastPlatformFetch", DateTime.UtcNow.AddDays(-31));
            DateTime lastFetchDateTime = lastPlatformFetchObj is DateTime dtP ? dtP : DateTime.UtcNow.AddDays(-31);

            // check if the last fetch date and time is older than the expiration time
            if (DateTime.UtcNow - lastFetchDateTime < TimeSpan.FromDays(TimeToExpire))
            {
                Logging.Log(Logging.LogType.Information, "GiantBomb", "Platforms are up to date. No need to download.");
                return;
            }

            // Log the start of the platform download process
            Logging.Log(Logging.LogType.Information, "GiantBomb", "Downloading platforms from GiantBomb.");

            // check if API key is set, otherwise exit
            if (!String.IsNullOrEmpty(Config.GiantBomb.APIKey))
            {
                int offset = 0;

                // repeat until we get no more platforms, incrementing offset by 100 each time
                while (true)
                {
                    // Fetch platforms from GiantBomb API
                    string url = $"/api/platforms/?api_key={Config.GiantBomb.APIKey}&format=json&limit=100&offset={offset}&filter=date_last_updated:{LastUpdate}|{UpdateEndDate}";
                    try
                    {
                        var json = await Task.Run(() => GetJson<Models.GiantBombPlatformResponse>(url));

                        if (json.results == null || json.results.Count == 0)
                        {
                            // No more platforms to fetch, exit the loop
                            break;
                        }

                        // Process each platform
                        foreach (var platform in json.results)
                        {
                            if (platform is GiantBomb.Models.Platform platformDict)
                            {
                                // Assuming you have a Database instance `db` and an object `myObj`:
                                await Classes.Metadata.MetadataStorage.StoreObjectWithSubclasses(platform, db, dbName, platform.id);

                                await ProcessImageTags(db, platform.guid, platform.image_tags);

                                // Log the platform processing
                                Logging.Log(Logging.LogType.Information, "GiantBomb", $"Processed platform: {platformDict.name}");
                            }
                        }

                        // cheak if there are more platforms to fetch
                        if (json.results.Count < 100)
                        {
                            // If the count is less than 100, we have fetched all platforms
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Error fetching platforms: {ex.Message}", ex);
                        break; // Exit the loop on error
                    }

                    // Increment offset for the next batch
                    offset += 100;

                    // To avoid hitting API rate limits, delay the next request
                    await Task.Delay(5000); // Sleep for 5 seconds
                }
            }

            // Update the last fetch date and time in settings
            SetTracking("GiantBomb_LastPlatformFetch", DateTime.UtcNow);

            Logging.Log(Logging.LogType.Information, "GiantBomb", "Finished downloading platforms from GiantBomb.");

            return;
        }

        /// <summary>
        /// Downloads game metadata for all platforms present in the database.
        /// </summary>
        public async Task DownloadGames()
        {
            // get the list of platforms from the database
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            var platforms = await db.ExecuteCMDAsync($"SELECT Id FROM {dbName}.Platform;");

            // check if there are any platforms to download games for
            if (platforms.Rows.Count == 0)
            {
                Logging.Log(Logging.LogType.Warning, "GiantBomb", "No platforms found to download games for.");
                return;
            }

            // download games for each platform
            foreach (DataRow row in platforms.Rows)
            {
                long platformId = Convert.ToInt64(row["Id"]);
                await _DownloadGames(platformId);
            }

            return;
        }

        private async Task _DownloadGames(long platformId)
        {
            // Ensure that the local file path exists
            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            // get the last game fetch date and time from settings
            var lastGameFetchObj = GetTracking($"GiantBomb_LastGameFetch-{platformId}", DateTime.UtcNow.AddDays(-31));
            DateTime lastFetchDateTime = lastGameFetchObj is DateTime dtG ? dtG : DateTime.UtcNow.AddDays(-31);

            // check if the last fetch date and time is older than the expiration time
            if (DateTime.UtcNow - lastFetchDateTime < TimeSpan.FromDays(TimeToExpire))
            {
                Logging.Log(Logging.LogType.Information, "GiantBomb", $"Games for platform ID {platformId} are up to date. No need to download.");
                return;
            }

            // setup database if it doesn't exist
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            // add indexes to the Game table
            // Add an index to the Name column
            string indexQuery = $"CREATE INDEX IF NOT EXISTS `idx_Game_name` ON `{dbName}`.`Game` (`name`); CREATE INDEX IF NOT EXISTS `idx_Game_aliases` ON `{dbName}`.`Game` (`aliases`);";
            Console.WriteLine($"Executing query: {indexQuery}");
            await db.ExecuteCMDAsync(indexQuery);

            // Add a Full Text Search index to the Name column
            // Check if the FULLTEXT index already exists before adding it
            string checkFullTextIndexQuery = $@"
                SELECT * 
                FROM information_schema.STATISTICS 
                WHERE table_schema = '{dbName}' 
                  AND table_name = 'Game' 
                  AND index_name = 'ft_idx_Game_name'";
            var fullTextIndexExists = Convert.ToInt32((await db.ExecuteCMDAsync(checkFullTextIndexQuery)).Rows.Count) > 0;

            string fullTextIndexQuery = $"ALTER TABLE `{dbName}`.`Game` ADD FULLTEXT INDEX `ft_idx_Game_name` (`name`); ALTER TABLE `{dbName}`.`Game` ADD FULLTEXT INDEX `ft_idx_Game_aliases` (`aliases`);";
            if (!fullTextIndexExists)
            {
                Console.WriteLine($"Executing query: {fullTextIndexQuery}");
                await db.ExecuteCMDAsync(fullTextIndexQuery);
            }
            else
            {
                Console.WriteLine("FullText index 'ft_idx_Game_name' already exists. Skipping creation.");
            }

            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Downloading games for platform ID {platformId} from GiantBomb.");

            // check if API key is set, otherwise exit
            if (!String.IsNullOrEmpty(Config.GiantBomb.APIKey))
            {
                int offset = 0;

                // get the current offset from settings, default to 0 if not set
                var offGameObj = GetTracking($"GiantBomb_GameOffset-{platformId}", 0);
                if (offGameObj is int ogi) offset = ogi; else if (offGameObj is long ogl) offset = (int)ogl; else if (!int.TryParse(offGameObj?.ToString(), out offset)) offset = 0;

                // Initialize the list to hold game results
                var games = new List<GiantBomb.Models.Game>();

                // check if the platform ID is valid
                if (platformId <= 0)
                {
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", "Invalid platform ID provided.");
                    return;
                }

                // repeat until we get no more games, incrementing offset by 100 each time
                while (true)
                {
                    // Fetch games from GiantBomb API
                    string url = $"/api/games/?api_key={Config.GiantBomb.APIKey}&format=json&limit=100&offset={offset}&platforms={platformId}&filter=date_last_updated:{LastUpdate}|{UpdateEndDate}";
                    try
                    {
                        var json = await GetJson<Models.GiantBombGameResponse>(url);

                        if (json.results == null || json.results.Count == 0)
                        {
                            // No more games to fetch, exit the loop
                            break;
                        }

                        // Process each game
                        foreach (var game in json.results)
                        {
                            // Here you can process the game as needed
                            // For example, you can log the game name or store it in a list
                            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Found game: {game.name}");
                            if (game is GiantBomb.Models.Game gameDict)
                            {
                                await Classes.Metadata.MetadataStorage.StoreObjectWithSubclasses(gameDict, db, dbName, game.id);

                                await ProcessImageTags(db, game.guid, game.image_tags);
                            }
                        }

                        // Store the offset in settings for the next batch
                        SetTracking($"GiantBomb_GameOffset-{platformId}", offset);

                        // Check if there are more games to fetch
                        if (json.results.Count < 100)
                        {
                            // If the count is less than 100, we have fetched all games
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Error downloading games for platform ID {platformId}: {ex.Message}", ex);
                        break;
                    }

                    // Increment offset for the next batch
                    offset += 100;

                    // To avoid hitting API rate limits, delay the next request
                    await Task.Delay(10000); // Sleep for 10 seconds
                }

                // reset the offset to 0 for the next fetch
                SetTracking($"GiantBomb_GameOffset-{platformId}", 0);
            }

            // Update the last fetch date and time in settings
            SetTracking($"GiantBomb_LastGameFetch-{platformId}", DateTime.UtcNow);

            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Finished downloading games for platform ID {platformId} from GiantBomb.");
        }

        private async Task ProcessImageTags(Database db, string guid, List<ImageTag> imageTags)
        {
            // check if guid was last checked more than 30 days ago
            // get the last game fetch date and time from settings
            var lastImageFetchObj = GetTracking($"GiantBomb_LastImageFetch-{guid}", DateTime.UtcNow.AddDays(-31));
            DateTime lastFetchDateTime = lastImageFetchObj is DateTime dtI ? dtI : DateTime.UtcNow.AddDays(-31);
            // check if the last fetch date and time is older than the expiration time
            if (DateTime.UtcNow - lastFetchDateTime < TimeSpan.FromDays(TimeToExpire))
            {
                Logging.Log(Logging.LogType.Information, "GiantBomb", $"Images for guid {guid} are up to date. No need to download.");
                return;
            }

            if (imageTags != null && imageTags.Count > 0)
            {
                // start downloading images for the guid
                await _DownloadImages(db, guid, imageTags);
            }

            // download complete, update the last fetch date and time in settings
            SetTracking($"GiantBomb_LastImageFetch-{guid}", DateTime.UtcNow);
            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Finished downloading images for guid {guid}.");
        }

        private async Task _DownloadImages(Database db, string guid, List<ImageTag> image_tags)
        {
            // capture images
            Dictionary<string, GiantBomb.Models.Image> images = new Dictionary<string, GiantBomb.Models.Image>();
            foreach (var imageTag in image_tags)
            {
                int offset = 0;

                while (true)
                {
                    string imageTagUrl = $"/api/images/{guid}/?api_key={Config.GiantBomb.APIKey}&format=json&limit=100&offset={offset}&filter=image_tag:{imageTag.name}";
                    try
                    {
                        var imageJson = await GetJson<Models.GiantBombImageResponse>(imageTagUrl);

                        if (imageJson == null || imageJson.results == null || imageJson.results.Count == 0)
                        {
                            // No more images to fetch, exit the loop
                            Logging.Log(Logging.LogType.Information, "GiantBomb", $"No more images found for guid: {guid} with image tag: {imageTag.name}");
                            break;
                        }

                        if (imageJson.results != null && imageJson.results.Count > 0)
                        {
                            foreach (var image in imageJson.results)
                            {
                                image.guid = guid; // Set the guid for the image

                                // Check if the image already exists in the dictionary
                                if (!images.ContainsKey(image.original_url))
                                {
                                    images[image.original_url] = image; // Add the image to the dictionary
                                }

                                // get the image from the dictionary
                                var existingImage = images[image.original_url];
                                // if the existing tags are blank, use the current one
                                if (string.IsNullOrEmpty(existingImage.image_tags))
                                {
                                    existingImage.image_tags = imageTag.name; // Set the image tag
                                }
                                else if (!existingImage.image_tags.Contains(imageTag.name))
                                {
                                    existingImage.image_tags += $",{imageTag.name}"; // Append the image tag
                                }
                                // write the image to the dictionary
                                images[image.original_url] = existingImage; // Update the image in the dictionary
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Error downloading images for guid {guid} with image tag {imageTag.name}: {ex.Message}", ex);
                        break;
                    }

                    offset += 100;
                }
            }
            // Store the images in the database
            foreach (var image in images.Values)
            {
                // delete any entries with the same guid to clear out old records
                string deleteQuery = $"DELETE FROM {dbName}.Image WHERE guid = @guid AND original_url = @original_url";
                await db.ExecuteCMDAsync(deleteQuery, new Dictionary<string, object>
                                {
                                    { "guid", image.guid },
                                    { "original_url", image.original_url }
                                });
                // insert the image into the database
                string insertQuery = $"INSERT INTO {dbName}.Image (guid, icon_url, medium_url, screen_url, screen_large_url, small_url, super_url, thumb_url, tiny_url, original_url, image_tags) VALUES (@guid, @icon_url, @medium_url, @screen_url, @screen_large_url, @small_url, @super_url, @thumb_url, @tiny_url, @original_url, @image_tags)";
                await db.ExecuteCMDAsync(insertQuery, new Dictionary<string, object>
                                {
                                    { "guid", image.guid },
                                    { "icon_url", image.icon_url },
                                    { "medium_url", image.medium_url },
                                    { "screen_url", image.screen_url },
                                    { "screen_large_url", image.screen_large_url },
                                    { "small_url", image.small_url },
                                    { "super_url", image.super_url },
                                    { "thumb_url", image.thumb_url },
                                    { "tiny_url", image.tiny_url },
                                    { "original_url", image.original_url },
                                    { "image_tags", image.image_tags }
                                });
            }

            return;
        }

        /// <summary>
        /// Generic downloader for subtype collections with optional filter query.
        /// </summary>
        public async Task DownloadSubTypes<TResponse, TObject>(string endpoint, string? query = "") where TResponse : class
        {
            string typeName = typeof(TObject).Name;

            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            string settingName = $"GiantBomb_{typeName}Fetch{"_" + query}";
            var lastSubtypeFetchObj = GetTracking(settingName, DateTime.UtcNow.AddDays(-31));
            DateTime lastFetchDateTime = lastSubtypeFetchObj is DateTime dtS ? dtS : DateTime.UtcNow.AddDays(-31);

            if (DateTime.UtcNow - lastFetchDateTime < TimeSpan.FromDays(TimeToExpire))
            {
                Logging.Log(Logging.LogType.Information, "GiantBomb", $"Data for {typeName} up to date. No need to download.");
                return;
            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Downloading {typeName} from GiantBomb.");

            if (!string.IsNullOrEmpty(Config.GiantBomb.APIKey))
            {
                int offset = 0;
                var offObj = GetTracking($"{settingName}Offset", 0);
                if (offObj is int oi) offset = oi; else if (offObj is long ol) offset = (int)ol; else if (!int.TryParse(offObj?.ToString(), out offset)) offset = 0;

                while (true)
                {
                    string url = $"/api/{endpoint}/?api_key={Config.GiantBomb.APIKey}&format=json&limit=100&offset={offset}{(string.IsNullOrEmpty(query) ? string.Empty : $"&filter={query}")}";
                    try
                    {
                        var json = await GetJson<TResponse>(url);
                        var resultsProp = typeof(TResponse).GetProperty("results");
                        var results = resultsProp?.GetValue(json) as IEnumerable<object>;
                        var resultsList = results?.Cast<object>().ToList();
                        if (resultsList == null || resultsList.Count == 0)
                            break;

                        foreach (var apiObject in resultsList)
                        {
                            var idProp = apiObject.GetType().GetProperty("id") ?? apiObject.GetType().GetProperty("Id");
                            var idValue = idProp?.GetValue(apiObject);
                            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Found {typeName}: {idValue}");
                            await Classes.Metadata.MetadataStorage.StoreObjectWithSubclasses(apiObject, db, dbName, idValue as long?);
                        }

                        SetTracking($"{settingName}Offset", offset);
                        if (resultsList.Count < 100)
                            break;
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Error downloading {typeName}: {ex.Message}", ex);
                        break;
                    }
                    offset += 100;
                    await Task.Delay(10000); // pacing
                }
                SetTracking($"{settingName}Offset", 0);
            }

            SetTracking(settingName, DateTime.UtcNow);
            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Finished downloading {typeName} from GiantBomb.");
        }

        private async Task<T> GetJson<T>(string url)
        {
            // Unified implementation: rate limit + retry semantics inline. _GetJson removed per request.
            CancellationToken ct = CancellationToken.None; // placeholder if future cancellation desired

            if (!headersConfigured)
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Hasheous", "1.0"));
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.ConnectionClose = false;
                headersConfigured = true;
            }

            // Normalize URL
            url = url.StartsWith("/") ? url : "/" + url;
            var absoluteUrl = new Uri(new Uri(BaseUrl.TrimEnd('/')), url);

            int transientRetry = 0;
            const int transientMax = 5; // for non-rate-limit transient errors (5xx)

            while (true)
            {
                await WaitForRateLimitAsync(ct).ConfigureAwait(false);
                Logging.Log(Logging.LogType.Information, "GiantBomb", $"Fetching JSON from {absoluteUrl}");

                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(absoluteUrl, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Network-level error: apply bounded retry with exponential backoff
                    transientRetry++;
                    if (transientRetry > transientMax)
                        throw new Exception($"Network error after {transientMax} retries: {ex.Message}", ex);
                    var wait = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, transientRetry)));
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Network error '{ex.Message}'. Retry {transientRetry}/{transientMax} in {wait.TotalSeconds}s.");
                    await Task.Delay(wait, ct).ConfigureAwait(false);
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(jsonString);
                        if (obj == null)
                            throw new Exception("Deserialized JSON was null.");
                        return obj;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Deserialization failure", ex);
                    }
                }

                // Extract Retry-After if present
                double? retryAfterSeconds = null;
                if (response.Headers.RetryAfter != null)
                {
                    retryAfterSeconds = response.Headers.RetryAfter.Delta?.TotalSeconds;
                    if (retryAfterSeconds == null && response.Headers.RetryAfter.Date.HasValue)
                        retryAfterSeconds = (response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds;
                }

                var status = (int)response.StatusCode;
                if (status == 420 || status == 429)
                {
                    // Infinite retry policy for 420 / 429
                    TimeSpan wait;
                    if (status == 420)
                    {
                        // Long cool-down: header value if available, else random 5-10 minutes
                        if (retryAfterSeconds.HasValue && retryAfterSeconds > 0)
                            wait = TimeSpan.FromSeconds(retryAfterSeconds.Value);
                        else
                            wait = TimeSpan.FromMinutes(5 + _rng.NextDouble() * 5); // 5-10 min
                        Logging.Log(Logging.LogType.Warning, "GiantBomb", $"420 secondary rate limit triggered. Waiting {wait.TotalMinutes:F1} minutes before retrying.");
                    }
                    else // 429
                    {
                        if (retryAfterSeconds.HasValue && retryAfterSeconds > 0)
                            wait = TimeSpan.FromSeconds(retryAfterSeconds.Value);
                        else
                        {
                            // Progressive backoff but capped (start modest; typical velocity limit)
                            transientRetry++;
                            var baseWait = Math.Min(120, Math.Pow(2, transientRetry)); // cap at 2 minutes
                            wait = TimeSpan.FromSeconds(baseWait);
                        }
                        Logging.Log(Logging.LogType.Warning, "GiantBomb", $"429 too many requests. Waiting {wait.TotalSeconds:F0}s before retrying.");
                    }
                    await Task.Delay(wait, ct).ConfigureAwait(false);
                    // For rate-limit responses we do not increment transientRetry beyond what 429 logic used above.
                    continue; // retry indefinitely
                }
                else if (status >= 500 && status < 600)
                {
                    // Transient server error: bounded retries
                    transientRetry++;
                    if (transientRetry > transientMax)
                        throw new Exception($"Server error {(int)response.StatusCode} after {transientMax} retries: {response.ReasonPhrase}");
                    var wait = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, transientRetry))); // exp backoff capped at 60s
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Server error {(int)response.StatusCode}. Retry {transientRetry}/{transientMax} in {wait.TotalSeconds}s.");
                    await Task.Delay(wait, ct).ConfigureAwait(false);
                    continue;
                }
                else if (status == 401 || status == 403 || status == 404)
                {
                    // Non-retryable (per instructions we "skip" by surfacing exception)
                    throw new Exception($"Non-retryable HTTP {status}: {response.ReasonPhrase}");
                }
                else
                {
                    // Other unexpected status; treat as non-retryable.
                    throw new Exception($"Unexpected HTTP {status}: {response.ReasonPhrase}");
                }
            }
        }
    }
}