using System.Data;
using Classes;

namespace GiantBomb
{
    public class MetadataDownload
    {
        private static readonly HttpClient client = new HttpClient();

        // check if Config.GiantBomb.BaseURL is set, otherwise use default
        string BaseUrl = Config.GiantBomb.BaseURL ?? "https://www.giantbomb.com/";

        public string LocalFilePath
        {
            get
            {
                return Config.LibraryConfiguration.LibraryMetadataDirectory_GiantBomb;
            }
        }

        public string dbName { get; set; } = "giantbomb";

        public int TimeToExpire { get; set; } = 30; // Default expiration time for cached data

        public static int MaximumRequestsPerMinute = 30;
        public static int RateLimitSleepTime = 10; // time in seconds to sleep when rate limit is hit
        private static int requestCount = 0;
        private static DateTime lastResetTime = DateTime.UtcNow;
        private async Task<bool> checkRequestLimit()
        {
            // Enforce a slow down mechanism to avoid hitting the API rate limit
            await Task.Delay(1000); // Sleep for 1 second between requests

            // Reset the request count every minute
            if ((DateTime.UtcNow - lastResetTime).TotalMinutes >= 1)
            {
                requestCount = 0;
                lastResetTime = DateTime.UtcNow;
            }
            // Check if the request count exceeds the maximum allowed requests per minute
            if (requestCount >= MaximumRequestsPerMinute)
            {
                Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Rate limit exceeded. Sleeping for {RateLimitSleepTime} seconds.");
                System.Threading.Thread.Sleep(RateLimitSleepTime * 1000);
                requestCount = 0;
                lastResetTime = DateTime.UtcNow;
                return false; // Indicate that the request limit was hit
            }
            requestCount++;
            return true; // Indicate that the request limit was not hit
        }


        public void DownloadPlatforms()
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
            db.ExecuteNonQuery(indexQuery);

            // Add a Full Text Search index to the Name column
            // Check if the FULLTEXT index already exists before adding it
            string checkFullTextIndexQuery = $@"
                SELECT * 
                FROM information_schema.STATISTICS 
                WHERE table_schema = '{dbName}' 
                  AND table_name = 'Platform' 
                  AND index_name = 'ft_idx_Platform_name'";
            var fullTextIndexExists = Convert.ToInt32(db.ExecuteCMD(checkFullTextIndexQuery).Rows.Count) > 0;

            string fullTextIndexQuery = $"ALTER TABLE `{dbName}`.`Platform` ADD FULLTEXT INDEX `ft_idx_Platform_name` (`name`); ALTER TABLE `{dbName}`.`Platform` ADD FULLTEXT INDEX `ft_idx_Platform_aliases` (`aliases`);";
            if (!fullTextIndexExists)
            {
                Console.WriteLine($"Executing query: {fullTextIndexQuery}");
                db.ExecuteNonQuery(fullTextIndexQuery);
            }
            else
            {
                Console.WriteLine("FullText index 'ft_idx_Game_name' already exists. Skipping creation.");
            }

            // get the last platform fetch date and time from settings
            DateTime lastFetchDateTime = Config.ReadSetting("GiantBomb_LastPlatformFetch", DateTime.UtcNow.AddDays(-31));

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
                    string url = $"/api/platforms/?api_key={Config.GiantBomb.APIKey}&format=json&limit=100&offset={offset}";
                    var json = GetJson<Models.GiantBombPlatformResponse>(url);

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
                            StoreObjectWithSubclasses(platform, db, dbName, platform.id);

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

                    // Increment offset for the next batch
                    offset += 100;

                    // To avoid hitting API rate limits, delay the next request
                    System.Threading.Thread.Sleep(5000); // Sleep for 5 seconds
                }
            }

            // Update the last fetch date and time in settings
            Config.SetSetting("GiantBomb_LastPlatformFetch", DateTime.UtcNow);

            Logging.Log(Logging.LogType.Information, "GiantBomb", "Finished downloading platforms from GiantBomb.");
        }

        public void DownloadGames()
        {
            // get the list of platforms from the database
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            var platforms = db.ExecuteCMD($"SELECT Id FROM {dbName}.Platform;");

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
                _DownloadGames(platformId);
            }
        }

        private void _DownloadGames(long platformId)
        {
            // Ensure that the local file path exists
            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            // get the last game fetch date and time from settings
            DateTime lastFetchDateTime = Config.ReadSetting($"GiantBomb_LastGameFetch-{platformId}", DateTime.UtcNow.AddDays(-31));

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
            db.ExecuteNonQuery(indexQuery);

            // Add a Full Text Search index to the Name column
            // Check if the FULLTEXT index already exists before adding it
            string checkFullTextIndexQuery = $@"
                SELECT * 
                FROM information_schema.STATISTICS 
                WHERE table_schema = '{dbName}' 
                  AND table_name = 'Game' 
                  AND index_name = 'ft_idx_Game_name'";
            var fullTextIndexExists = Convert.ToInt32(db.ExecuteCMD(checkFullTextIndexQuery).Rows.Count) > 0;

            string fullTextIndexQuery = $"ALTER TABLE `{dbName}`.`Game` ADD FULLTEXT INDEX `ft_idx_Game_name` (`name`); ALTER TABLE `{dbName}`.`Game` ADD FULLTEXT INDEX `ft_idx_Game_aliases` (`aliases`);";
            if (!fullTextIndexExists)
            {
                Console.WriteLine($"Executing query: {fullTextIndexQuery}");
                db.ExecuteNonQuery(fullTextIndexQuery);
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
                offset = int.Parse(Config.ReadSetting($"GiantBomb_GameOffset-{platformId}", 0).ToString());

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
                    string url = $"/api/games/?api_key={Config.GiantBomb.APIKey}&format=json&limit=100&offset={offset}&platforms={platformId}";
                    var json = GetJson<Models.GiantBombGameResponse>(url);

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
                            StoreObjectWithSubclasses(gameDict, db, dbName, game.id);
                        }
                    }

                    // Store the offset in settings for the next batch
                    Config.SetSetting($"GiantBomb_GameOffset-{platformId}", offset);

                    // Check if there are more games to fetch
                    if (json.results.Count < 100)
                    {
                        // If the count is less than 100, we have fetched all games
                        break;
                    }

                    // Increment offset for the next batch
                    offset += 100;

                    // To avoid hitting API rate limits, delay the next request
                    System.Threading.Thread.Sleep(10000); // Sleep for 10 seconds
                }

                // reset the offset to 0 for the next fetch
                Config.SetSetting($"GiantBomb_GameOffset-{platformId}", 0);
            }

            // Update the last fetch date and time in settings
            Config.SetSetting($"GiantBomb_LastGameFetch-{platformId}", DateTime.UtcNow);

            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Finished downloading games for platform ID {platformId} from GiantBomb.");
        }

        public void DownloadSubTypes<TResponse, TObject>(string endpoint, string? query = "")
            where TResponse : class
        {
            // get the name of TObject
            string typeName = typeof(TObject).Name;

            // ensure that the local file path exists
            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            // get the last fetch date and time from settings
            string settingName = $"GiantBomb_{typeName}Fetch{"_" + query}";
            DateTime lastFetchDateTime = Config.ReadSetting(settingName, DateTime.UtcNow.AddDays(-31));

            // check if the last fetch date and time is older than the expiration time
            if (DateTime.UtcNow - lastFetchDateTime < TimeSpan.FromDays(TimeToExpire))
            {
                Logging.Log(Logging.LogType.Information, "GiantBomb", $"Data for {typeName} up to date. No need to download.");
                return;
            }

            // setup database if it doesn't exist
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Downloading {typeName} from GiantBomb.");

            // check if API key is set, otherwise exit
            if (!String.IsNullOrEmpty(Config.GiantBomb.APIKey))
            {
                int offset = 0;

                // get the current offset from settings, default to 0 if not set
                offset = int.Parse(Config.ReadSetting($"{settingName}Offset", 0).ToString());

                // repeat until we get no more objects, incrementing offset by 100 each time
                while (true)
                {
                    // Fetch objects from GiantBomb API
                    string url = $"/api/{endpoint}/?api_key={Config.GiantBomb.APIKey}&format=json&limit=100&offset={offset}{(query != null && query != "" ? $"&filter={query}" : "")}";
                    var json = GetJson<TResponse>(url);

                    // Use reflection to get the 'results' property
                    var resultsProp = typeof(TResponse).GetProperty("results");
                    var results = resultsProp?.GetValue(json) as IEnumerable<object>;

                    if (results == null || !results.Cast<object>().Any())
                    {
                        // No more objects to fetch, exit the loop
                        break;
                    }

                    // Process each object
                    foreach (var apiObject in results)
                    {
                        // Here you can process the object as needed
                        // For example, you can log the object id or store it in a list
                        var idProp = apiObject.GetType().GetProperty("id") ?? apiObject.GetType().GetProperty("Id");
                        var idValue = idProp?.GetValue(apiObject);
                        Logging.Log(Logging.LogType.Information, "GiantBomb", $"Found {typeName}: {idValue}");
                        StoreObjectWithSubclasses(apiObject, db, dbName, idValue as long?);
                    }

                    // Store the offset in settings for the next batch
                    Config.SetSetting($"{settingName}Offset", offset);

                    // Check if there are more objects to fetch
                    if (results.Count() < 100)
                    {
                        // If the count is less than 100, we have fetched all objects
                        break;
                    }

                    // Increment offset for the next batch
                    offset += 100;

                    // To avoid hitting API rate limits, delay the next request
                    System.Threading.Thread.Sleep(10000); // Sleep for 10 seconds
                }

                // reset the offset to 0 for the next fetch
                Config.SetSetting($"{settingName}Offset", 0);
            }

            // Update the last fetch date and time in settings
            Config.SetSetting(settingName, DateTime.UtcNow);

            Logging.Log(Logging.LogType.Information, "GiantBomb", $"Finished downloading {typeName} from GiantBomb.");
        }

        private T GetJson<T>(string url)
        {
            checkRequestLimit();

            int maxRetries = 5;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    return _GetJson<T>(url);
                }
                catch (Exception ex)
                {
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", $"Error fetching JSON from {url}: {ex.Message}", ex);
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        throw new Exception($"Failed to fetch JSON after {maxRetries} attempts.", ex);
                    }
                    Logging.Log(Logging.LogType.Information, "GiantBomb", $"Retrying... ({retryCount}/{maxRetries})");

                    // Wait before retrying
                    // This is to avoid hitting API rate limits or transient errors
                    // Adjust the sleep time as necessary
                    int sleepTime = (int)Math.Pow(2, retryCount) * 10000; // Exponential backoff
                    Logging.Log(Logging.LogType.Information, "GiantBomb", $"Sleeping for {sleepTime / 1000} seconds before retrying.");

                    // Sleep for a while before retrying
                    System.Threading.Thread.Sleep(sleepTime); // Wait before retrying
                }
            }

            throw new Exception("Max retries reached without success.");
        }

        private T _GetJson<T>(string url)
        {
            try
            {
                // reset the client
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Hasheous", "1.0"));
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.ConnectionClose = false; // Keep the connection alive

                // make the request
                url = url.StartsWith("/") ? url : "/" + url; // Ensure the URL starts with a slash
                url = BaseUrl.TrimEnd('/') + url; // Ensure the base URL is properly formatted
                Logging.Log(Logging.LogType.Information, "GiantBomb", $"Fetching JSON from {url}");

                var response = client.GetAsync(new Uri(new Uri(BaseUrl), new Uri(url))).Result;
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = response.Content.ReadAsStringAsync().Result;
                    T jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(jsonString);
                    return jsonResponse;
                }
                else if (response.StatusCode == (System.Net.HttpStatusCode)420) // Rate limit exceeded
                {
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", "Rate limit exceeded. Please wait before retrying.");

                    // Check if the response contains a Retry-After header
                    if (response.Headers.RetryAfter != null)
                    {
                        var retryAfter = response.Headers.RetryAfter.Delta?.TotalSeconds ?? 300; // Default to 300 seconds if not specified
                        Logging.Log(Logging.LogType.Information, "GiantBomb", $"Retry after: {retryAfter} seconds");
                        System.Threading.Thread.Sleep((int)retryAfter * 1000); // Sleep for the specified time
                    }
                    else
                    {
                        Logging.Log(Logging.LogType.Information, "GiantBomb", "No Retry-After header found. Defaulting to 300 seconds.");
                        System.Threading.Thread.Sleep(300000); // Default to 5 minutes if no Retry-After header is present
                    }

                    throw new Exception("Rate limit exceeded. Please wait before retrying.");
                }
                else if (response.StatusCode == (System.Net.HttpStatusCode)429) // Too many requests
                {
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", "Too many requests. Please wait before retrying.");
                    throw new Exception("Too many requests. Please wait before retrying.");
                }
                else if (response.StatusCode == (System.Net.HttpStatusCode)401) // Unauthorized
                {
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", "Unauthorized access. Please check your API key.");
                    throw new Exception("Unauthorized access. Please check your API key.");
                }
                else if (response.StatusCode == (System.Net.HttpStatusCode)403) // Forbidden
                {
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", "Forbidden access. You do not have permission to access this resource.");
                    throw new Exception("Forbidden access. You do not have permission to access this resource.");
                }
                else if (response.StatusCode == (System.Net.HttpStatusCode)404) // Not found
                {
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", "Resource not found.");
                    throw new Exception("Resource not found.");
                }
                else if (response.StatusCode == (System.Net.HttpStatusCode)500) // Internal server error
                {
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", "Internal server error. Please try again later.");
                    throw new Exception("Internal server error. Please try again later.");
                }
                else if (response.StatusCode == (System.Net.HttpStatusCode)503) // Service unavailable
                {
                    Logging.Log(Logging.LogType.Warning, "GiantBomb", "Service unavailable. Please try again later.");
                    throw new Exception("Service unavailable. Please try again later.");
                }
                else
                {
                    throw new Exception($"Error fetching data: {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception occurred while fetching JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Stores an object and its subclasses in the database.
        /// The table name is the type name. Subclasses are stored in their own tables and linked by Id.
        /// Subclasses without an Id property will be assigned one by the database (auto-increment).
        /// </summary>
        private void StoreObjectWithSubclasses(object obj, Database db, string dbName, long? id = null)
        {
            if (obj == null) return;

            Type objType = obj.GetType();
            string tableName = objType.Name;

            var properties = objType.GetProperties();
            var parameters = new Dictionary<string, object>();
            object idValue = null;

            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj);

                // If property is a class (not string), treat as subclass
                if (value != null && prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    // check if the property type is a collection or array
                    if (prop.PropertyType.IsGenericType && prop.PropertyType.GetInterfaces().Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)) && prop.PropertyType != typeof(string))
                    {
                        // check if subclassess in the collection have an Id property
                        var subIdProp = prop.PropertyType.GetGenericArguments()[0].GetProperty("id") ?? prop.PropertyType.GetGenericArguments()[0].GetProperty("Id");
                        if (subIdProp == null)
                        {
                            // serialize the collection to JSON if no Id property exists
                            string jsonValue = Newtonsoft.Json.JsonConvert.SerializeObject(value, new Newtonsoft.Json.JsonSerializerSettings
                            {
                                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                MaxDepth = 30
                            });
                            parameters[prop.Name] = jsonValue;
                        }
                        else
                        {
                            // make sure the relationship table exists
                            string relationshipTableName = $"Relation_{tableName}_{prop.Name}";
                            string createTableSql = $@"CREATE TABLE IF NOT EXISTS {dbName}.{relationshipTableName} (
                                {tableName}_id BIGINT NOT NULL,
                                {prop.Name}_id BIGINT NOT NULL,
                                PRIMARY KEY ({tableName}_id, {prop.Name}_id),
                                INDEX idx_{tableName}_id ({tableName}_id),
                                INDEX idx_{prop.Name}_id ({prop.Name}_id)
                            );";
                            db.ExecuteCMD(createTableSql, new Dictionary<string, object>());

                            // remove all existing relationships for this object
                            string deleteSql = $"DELETE FROM {dbName}.{relationshipTableName} WHERE {tableName}_id = @id";
                            db.ExecuteCMD(deleteSql, new Dictionary<string, object> { { "id", id } });

                            // store a JSON array of Ids for the collection
                            var ids = new List<object>();
                            foreach (var item in (IEnumerable<object>)value)
                            {
                                var subId = subIdProp.GetValue(item);
                                ids.Add(subId ?? DBNull.Value);

                                // insert the relationship into the relationship table
                                if (subId != null)
                                {
                                    string insertSql = $"INSERT INTO {dbName}.{relationshipTableName} ({tableName}_id, {prop.Name}_id) VALUES (@{tableName}_id, @{prop.Name}_id)";
                                    db.ExecuteCMD(insertSql, new Dictionary<string, object>
                                    {
                                        { $"{tableName}_id", id },
                                        { $"{prop.Name}_id", subId }
                                    });
                                }
                            }

                            // store the JSON array of Ids
                            string jsonArray = Newtonsoft.Json.JsonConvert.SerializeObject(ids, new Newtonsoft.Json.JsonSerializerSettings
                            {
                                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                MaxDepth = 30
                            });
                            parameters[prop.Name] = jsonArray;
                        }
                    }
                    else
                    {

                        var subIdProp = prop.PropertyType.GetProperty("id") ?? prop.PropertyType.GetProperty("Id");
                        if (subIdProp != null)
                        {
                            StoreObjectWithSubclasses(value, db, dbName);

                            var subId = subIdProp.GetValue(value);
                            parameters[prop.Name] = subId ?? DBNull.Value;
                        }
                        else
                        {
                            // serialize the object to JSON if no Id property exists
                            string jsonValue = Newtonsoft.Json.JsonConvert.SerializeObject(value, new Newtonsoft.Json.JsonSerializerSettings
                            {
                                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                MaxDepth = 30
                            });
                            parameters[prop.Name] = jsonValue;
                        }
                    }
                }
                else
                {
                    parameters[prop.Name] = value ?? DBNull.Value;
                    if (prop.Name.Equals("id", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    {
                        idValue = value;
                    }
                }
            }

            // Check if object exists (by Id)
            bool exists = false;
            if (idValue != null)
            {
                string checkSql = $"SELECT `id` FROM {dbName}.{tableName} WHERE id = @id";
                var result = db.ExecuteCMD(checkSql, new Dictionary<string, object> { { "id", idValue } });
                exists = result.Rows.Count > 0;
            }

            // Build SQL
            string sql;
            if (exists)
            {
                // Update
                var setClause = string.Join(", ", parameters.Keys.Where(k => !k.Equals("id", StringComparison.OrdinalIgnoreCase)).Select(k => $"`{k}` = @{k}"));
                sql = $"UPDATE {dbName}.{tableName} SET {setClause} WHERE id = @id";

                db.ExecuteCMD(sql, parameters);
            }
            else
            {
                // Insert
                var cols = string.Join(", ", parameters.Keys.Select(k => $"`{k}`"));
                var vals = string.Join(", ", parameters.Keys.Select(k => $"@{k}"));
                sql = $"INSERT INTO {dbName}.{tableName} ({cols}) VALUES ({vals});";

                // Execute insert
                var result = db.ExecuteCMD(sql, parameters);
            }
        }
    }
}