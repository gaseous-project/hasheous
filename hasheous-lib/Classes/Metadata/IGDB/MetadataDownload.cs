using System.Collections;
using System.Text.RegularExpressions;
using Classes;
using InternetGameDatabase.Models;
using Microsoft.AspNetCore.Identity;

namespace InternetGameDatabase
{
    public class DownloadManager
    {
        private static readonly HttpClient client = new HttpClient();

        public string DumpsUrl
        {
            get
            {
                return "https://api.igdb.com/v4/dumps";
            }
        }

        public string LocalFilePath
        {
            get
            {
                return Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB, "Dumps");
            }
        }

        public string ExistingDumpsFilePath
        {
            get
            {
                return Path.Combine(LocalFilePath, "existing_dumps.json");
            }
        }

        public int MaxAgeInDays { get; set; } = 2;

        public bool IsLocalCopyOlderThanMaxAge(string LocalFileName)
        {
            if (!File.Exists(LocalFileName))
            {
                return true;
            }

            var lastWriteTime = File.GetLastWriteTime(LocalFileName);
            var age = DateTime.Now - lastWriteTime;
            return age.TotalDays > MaxAgeInDays;
        }

        private static AuthenticationToken AuthToken = new AuthenticationToken();

        public async Task Download()
        {
            // ensure target directory exists
            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            // create the database if it does not exist
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionStringNoDatabase);
            string sql = "CREATE DATABASE IF NOT EXISTS `igdb`";
            db.ExecuteNonQuery(sql);

            bool downloadDumps = true;
            if (!IsLocalCopyOlderThanMaxAge(ExistingDumpsFilePath))
            {
                // no need to download if the local copy is not older than the maximum age
                Logging.Log(Logging.LogType.Information, "IGDB Dumps", "Local copy of dumps is not older than the maximum age, skipping download.");
                downloadDumps = false;
            }

            // check if the locally cached authentication token is still valid
            bool tokenValid = false;
            if (AuthToken != null && AuthToken.expires_at.HasValue)
            {
                // check if the token is still valid
                if (AuthToken.expires_at.Value > DateTime.UtcNow)
                {
                    tokenValid = true;
                    Logging.Log(Logging.LogType.Information, "IGDB Dumps", "Using cached authentication token.");
                }
                else
                {
                    Logging.Log(Logging.LogType.Information, "IGDB Dumps", "Cached authentication token has expired, requesting a new one.");
                }
            }

            if (!tokenValid)
            {
                // token is not valid or does not exist, we need to request a new one
                Logging.Log(Logging.LogType.Information, "IGDB Dumps", "Requesting new authentication token from IGDB API...");

                // ensure the client is configured correctly
                client.DefaultRequestHeaders.Clear();

                // request igdb authentication token from the API
                // send a POST request to https://id.twitch.tv/oauth2/token?client_id=<CLIENTID>&client_secret=<CLIENTSECRET>&grant_type=client_credentials
                // add the query parameters to the URL
                // client_id, client_secret, and grant_type (grant_type should be set to "client_credentials")
                var tokenUrl = $"https://id.twitch.tv/oauth2/token?client_id={Config.IGDB.ClientId}&client_secret={Config.IGDB.Secret}&grant_type=client_credentials";

                // send the request
                var response = await client.PostAsync(tokenUrl, null);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Failed to authenticate with IGDB API.");
                }

                // read the response content
                // response is a JSON object - load it into the dictionary AuthToken
                var content = await response.Content.ReadAsStringAsync();
                AuthToken = System.Text.Json.JsonSerializer.Deserialize<AuthenticationToken>(content);

                if (AuthToken == null || AuthToken.access_token == null || AuthToken.access_token.Length == 0)
                {
                    throw new Exception("Failed to retrieve access token from IGDB API.");
                }

                // log the successful authentication
                Logging.Log(Logging.LogType.Information, "IGDB Dumps", "Successfully authenticated with IGDB API. Access token retrieved.");
            }

            List<DumpsModel> dumps = new List<DumpsModel>();
            if (downloadDumps)
            {
                // now we have a valid token, we can download the dumps
                // send a GET request to the dumps URL with the access token in the Authorization header and the Client-ID in the X-Client-ID header
                Logging.Log(Logging.LogType.Information, "IGDB Dumps", "Downloading dumps from IGDB API...");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken.access_token);
                client.DefaultRequestHeaders.Add("Client-ID", Config.IGDB.ClientId);

                // send the request
                var dumpsResponse = await client.GetAsync(DumpsUrl);

                if (!dumpsResponse.IsSuccessStatusCode)
                {
                    throw new Exception("Failed to retrieve dumps from IGDB API.");
                }

                // read the response content
                var dumpsContent = await dumpsResponse.Content.ReadAsStringAsync();

                // dumpsContent is a JSON array of DumpsModel objects
                dumps = System.Text.Json.JsonSerializer.Deserialize<List<InternetGameDatabase.Models.DumpsModel>>(dumpsContent);

                if (dumps == null || dumps.Count == 0)
                {
                    throw new Exception("No dumps found in IGDB API response.");
                }
            }
            else
            {
                // load the dumps from the existing dumps index - this is to ensure we do not download the same dumps again if they already exist
                if (!File.Exists(ExistingDumpsFilePath))
                {
                    throw new Exception("Existing dumps index file does not exist. Please download the dumps first.");
                }

                // log that we are using the existing dumps index
                Logging.Log(Logging.LogType.Information, "IGDB Dumps", "Using existing dumps index, no new dumps will be downloaded.");
                var existingDumpsContent = File.ReadAllText(ExistingDumpsFilePath);
                dumps = System.Text.Json.JsonSerializer.Deserialize<List<InternetGameDatabase.Models.DumpsModel>>(existingDumpsContent) ?? new List<InternetGameDatabase.Models.DumpsModel>();
                if (dumps.Count == 0)
                {
                    throw new Exception("No existing dumps found in the local index.");
                }
            }

            // load existing dumps from the local file system
            var existingDumpsIndex = new List<InternetGameDatabase.Models.DumpsModel>();
            if (File.Exists(ExistingDumpsFilePath))
            {
                var existingDumpsContent = File.ReadAllText(ExistingDumpsFilePath);
                existingDumpsIndex = System.Text.Json.JsonSerializer.Deserialize<List<InternetGameDatabase.Models.DumpsModel>>(existingDumpsContent) ?? new List<InternetGameDatabase.Models.DumpsModel>();
            }

            // download each dump if it does not exist in existingDumpsIndex and updated_at is different
            foreach (var dump in dumps)
            {
                // configure the client for the current dump
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken.access_token);
                client.DefaultRequestHeaders.Add("Client-ID", Config.IGDB.ClientId);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; curl/8.0; +https://curl.se/)");

                Logging.Log(Logging.LogType.Information, "IGDB Dumps", $"Processing dump: {dump.endpoint} - {dump.file_name}");

                if (dump.endpoint == null || dump.file_name == null)
                {
                    continue; // skip invalid dumps
                }

                // check if the dump.endpoint already exists in the existingDumpsIndex
                var existingDump = existingDumpsIndex.FirstOrDefault(d => d.endpoint == dump.endpoint);

                bool downloadDump = false;
                var dumpFilePath = Path.Combine(LocalFilePath, dump.endpoint + ".json");
                if (existingDump != null)
                {
                    // check if the updated_at date is different or if the file does not exist locally
                    if (existingDump.updated_at != dump.updated_at || !File.Exists(dumpFilePath))
                    {
                        // download the dump
                        downloadDump = true;
                    }
                }
                else
                {
                    // dump does not exist in the existingDumpsIndex, we need to download it
                    downloadDump = true;
                }

                bool forceDatabaseImport = false;
                if (downloadDump)
                {
                    forceDatabaseImport = true; // we will force the database import if we download a new dump

                    // download the dump file
                    // the dump file is located at the endpoint URL and contains the URL to download the actual data from
                    var dumpResponseUrl = $"https://api.igdb.com/v4/dumps/{dump.endpoint}";

                    // send the request
                    var dumpResponse = await client.GetAsync(dumpResponseUrl);

                    if (!dumpResponse.IsSuccessStatusCode)
                    {
                        Logging.Log(Logging.LogType.Warning, "IGDB Dumps", $"Failed to retrieve dump from {dumpResponseUrl}.");
                        continue; // skip this dump
                    }

                    // read the response content
                    var dumpContent = await dumpResponse.Content.ReadAsStringAsync();

                    // store the dump response content in a file
                    File.WriteAllText(dumpFilePath, dumpContent);

                    // dumpContent is a JSON object with the s3_url to download the actual data from
                    var dumpsResponseModel = System.Text.Json.JsonSerializer.Deserialize<InternetGameDatabase.Models.DumpsResponseModel>(dumpContent);

                    if (dumpsResponseModel == null || dumpsResponseModel.s3_url == null)
                    {
                        Logging.Log(Logging.LogType.Warning, "IGDB Dumps", $"Failed to retrieve s3_url from dump response for {dump.endpoint}.");
                        continue; // skip this dump
                    }

                    // download the actual data from the s3_url
                    var s3Url = dumpsResponseModel.s3_url;
                    var fileName = dump.endpoint + ".csv.tmp";
                    var filePath = Path.Combine(LocalFilePath, fileName);
                    Logging.Log(Logging.LogType.Information, "IGDB Dumps", $"Downloading dump data to {filePath}...");

                    if (File.Exists(filePath))
                    {
                        // if the file already exists, delete it
                        File.Delete(filePath);
                    }

                    // reset the headers for s3 download
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; curl/8.0; +https://curl.se/)");
                    System.Net.Http.HttpResponseMessage? dataResponse;
                    try
                    {
                        dataResponse = await client.GetAsync(s3Url);
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "IGDB Dumps", $"Exception occurred while downloading data from {s3Url}: {ex.Message}");
                        continue; // skip this dump
                    }

                    if (!dataResponse.IsSuccessStatusCode)
                    {
                        Logging.Log(Logging.LogType.Warning, "IGDB Dumps", $"Failed to download data from {s3Url}.");
                        continue; // skip this dump
                    }

                    // read the response content and save it to the file
                    var dataContent = await dataResponse.Content.ReadAsByteArrayAsync();
                    File.WriteAllBytes(filePath, dataContent);

                    // update the existing dumps index with the new dump information
                    if (existingDump != null)
                    {
                        existingDump.file_name = dump.file_name;
                        existingDump.updated_at = dump.updated_at;
                    }
                    else
                    {
                        existingDumpsIndex.Add(new InternetGameDatabase.Models.DumpsModel
                        {
                            endpoint = dump.endpoint,
                            file_name = dump.file_name,
                            updated_at = dump.updated_at
                        });
                    }

                    // save the updated existing dumps index to the file system
                    File.WriteAllText(ExistingDumpsFilePath, System.Text.Json.JsonSerializer.Serialize(existingDumpsIndex));

                    // rename the downloaded file to remove the .tmp extension
                    var finalFilePath = Path.Combine(LocalFilePath, dump.endpoint + ".csv");
                    if (File.Exists(finalFilePath))
                    {
                        File.Delete(finalFilePath); // delete the existing file if it exists
                    }
                    File.Move(filePath, finalFilePath);

                    Logging.Log(Logging.LogType.Information, "IGDB Dumps", $"Successfully downloaded and saved dump data to {finalFilePath}.");
                }

                // check if the table exists in the database
                db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                string tableName = "`igdb`.`" + dump.endpoint.Replace("-", "_") + "`";
                sql = $"SELECT * FROM information_schema.tables WHERE table_schema = 'igdb' AND table_name = '{dump.endpoint.Replace("-", "_")}' LIMIT 1;";
                if (db.ExecuteCMD(sql).Rows.Count == 0)
                {
                    // table does not exist, we need to create it
                    Logging.Log(Logging.LogType.Information, "IGDB Dumps", $"Table {tableName} does not exist in the database, creating it...");
                    forceDatabaseImport = true;
                }

                if (forceDatabaseImport)
                {
                    // import the dump into the database
                    Logging.Log(Logging.LogType.Information, "IGDB Dumps", $"Importing dump data into database table {tableName}...");

                    // drop the table if it exists
                    sql = $"DROP TABLE IF EXISTS {tableName}";
                    db.ExecuteNonQuery(sql);

                    // create the table based on the schema from the dump file
                    if (File.Exists(dumpFilePath))
                    {
                        var schema = System.Text.Json.JsonSerializer.Deserialize<DumpsResponseModel>(File.ReadAllText(dumpFilePath));
                        if (schema != null)
                        {
                            // create the table based on the schema
                            sql = $"CREATE TABLE {tableName} (";
                            string indexes = "";
                            foreach (var column in schema.schema.Keys)
                            {
                                // determine the column type based on the schema
                                string? columnType = "";
                                switch (schema.schema[column].ToString())
                                {
                                    case "INTEGER":
                                        columnType = "INT"; // use INT for INTEGER
                                        break;

                                    case "INTEGER[]":
                                        columnType = "JSON"; // use JSON for INTEGER arrays
                                        break;

                                    case "BOOLEAN":
                                        columnType = "TINYINT(1)"; // use TINYINT for BOOLEAN
                                        break;

                                    case "FLOAT":
                                        columnType = "FLOAT"; // use FLOAT for FLOAT
                                        break;

                                    case "LONG":
                                        columnType = "BIGINT"; // use BIGINT for LONG
                                        break;

                                    case "STRING":
                                        if (column == "name" || column == "slug")
                                        {
                                            columnType = "VARCHAR(255)"; // use VARCHAR for STRING
                                        }
                                        else
                                        {
                                            columnType = "TEXT"; // use TEXT for other STRING types
                                        }
                                        break;

                                    case "LONG[]":
                                        columnType = "JSON"; // use JSON for LONG arrays
                                        break;

                                    case "DOUBLE":
                                        columnType = "DOUBLE"; // use DOUBLE for DOUBLE
                                        break;

                                    case "TIMESTAMP":
                                        columnType = "DATETIME"; // use DATETIME for TIMESTAMP
                                        break;

                                    case "UUID":
                                        columnType = "VARCHAR(36)"; // use VARCHAR for UUID
                                        break;

                                    default:
                                        columnType = "VARCHAR(255)"; // default to VARCHAR for unknown types
                                        break;
                                }

                                sql += $"`{column}` {columnType}";

                                switch (column)
                                {
                                    case "id":
                                        sql += " PRIMARY KEY"; // set id as primary key
                                        break;

                                    case "name":
                                        // add fulltext index for name
                                        indexes += "FULLTEXT KEY `ft_name` (`name`), INDEX `idx_name` (`name` ASC) VISIBLE,";
                                        break;

                                    case "slug":
                                        // add index for slug
                                        indexes += "INDEX `idx_slug` (`slug` ASC) VISIBLE,";
                                        break;
                                }

                                sql += ",";
                            }
                            if (indexes.Length > 0)
                            {
                                sql += indexes.TrimEnd(',') + ""; // remove the last comma from indexes
                            }
                            sql = sql.TrimEnd(',') + ");"; // remove the last comma and close the statement
                            db.ExecuteNonQuery(sql);

                            // import the data from the CSV file into the database table
                            var csvFilePath = Path.Combine(LocalFilePath, dump.endpoint + ".csv");
                            if (File.Exists(csvFilePath))
                            {
                                // file is not local to the database, we need to insert the data manually
                                Logging.Log(Logging.LogType.Information, "IGDB Dumps", $"Importing data from {csvFilePath} into database table {tableName}...");

                                // read the CSV file and insert the data into the database
                                var csvFile = File.ReadAllText(csvFilePath);

                                // split the CSV file into lines using a regex to handle quoted fields
                                var lines = SplitCSVIntoLines(csvFile);

                                if (lines.Length > 0)
                                {
                                    // ensure the CSVParser is available
                                    Regex CSVParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

                                    // get the column names from the first line
                                    string[] columns = CSVParser.Split(lines[0]);

                                    for (int i = 1; i < lines.Length; i++)
                                    {
                                        String[] Fields = CSVParser.Split(lines[i]);

                                        if (Fields.Length != columns.Length)
                                        {
                                            Logging.Log(Logging.LogType.Warning, "IGDB Dumps", $"Skipping line {i + 1} in {csvFilePath} due to column count mismatch.");
                                            continue; // skip this line if the column count does not match
                                        }

                                        // build the insert statement
                                        sql = $"INSERT INTO {tableName} (";
                                        string columnValueParams = "";
                                        Dictionary<string, object?> columnValues = new Dictionary<string, object?>();
                                        for (int j = 0; j < columns.Length; j++)
                                        {
                                            sql += $"`{columns[j]}`";
                                            columnValueParams += $"@{columns[j]}";

                                            string columnValue = Fields[j];
                                            if (columnValue.StartsWith("\"") && columnValue.EndsWith("\""))
                                            {
                                                // remove the quotes from the column value
                                                columnValue = columnValue.Trim('"');
                                            }

                                            // add the column value to the dictionary
                                            if (Fields[j] == "null" || Fields[j] == "")
                                            {
                                                columnValues.Add(columns[j], null); // handle null values
                                            }
                                            else
                                            {
                                                switch (schema.schema[columns[j]].ToString())
                                                {
                                                    case "BOOLEAN":
                                                        // if columnValue is any value other than "0" or "1", treat it as true
                                                        columnValues.Add(columns[j], columnValue == "1" || columnValue.ToLower() == "true");
                                                        break;

                                                    case "INTEGER[]":
                                                    case "LONG[]":
                                                        columnValues.Add(columns[j], columnValue);

                                                        // these are arrays, but the source is wrapping them in curly braces, so need to replace them with square brackets
                                                        if (columnValue.StartsWith("{") && columnValue.EndsWith("}"))
                                                        {
                                                            columnValues[columns[j]] = "[" + columnValue.Trim('{', '}') + "]";
                                                        }
                                                        break;

                                                    default:
                                                        // for all other types, just add the value as a string
                                                        columnValues.Add(columns[j], columnValue);
                                                        break;
                                                }
                                            }

                                            if (j < columns.Length - 1)
                                            {
                                                sql += ", ";
                                                columnValueParams += ", ";
                                            }
                                        }

                                        sql += $") VALUES ({columnValueParams});";

                                        // execute the insert statement
                                        db.ExecuteNonQuery(sql, columnValues);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // save a flag to indicate that the dumps have been downloaded and processed
            File.WriteAllText(Path.Combine(LocalFilePath, "dumps_downloaded.flag"), DateTime.UtcNow.ToString("o"));

            // log the completion of the download
            Logging.Log(Logging.LogType.Information, "IGDB Dumps", "Dumps download and import completed successfully.");
        }

        private string[] SplitCSVIntoLines(string csvContent)
        {
            // Correctly handle newlines inside quoted fields in CSV.
            var lines = new List<string>();
            using (var reader = new StringReader(csvContent))
            {
                string? line;
                string? currentLine = null;
                bool insideQuotes = false;

                while ((line = reader.ReadLine()) != null)
                {
                    if (currentLine == null)
                        currentLine = line;
                    else
                        currentLine += "\n" + line;

                    // Count the number of quotes to determine if we're inside a quoted field
                    int quoteCount = 0;
                    for (int i = 0; i < line.Length; i++)
                    {
                        if (line[i] == '"')
                        {
                            // If it's a double quote, skip the next character
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                i++;
                                continue;
                            }
                            quoteCount++;
                        }
                    }

                    insideQuotes ^= (quoteCount % 2 != 0);

                    if (!insideQuotes)
                    {
                        lines.Add(currentLine);
                        currentLine = null;
                    }
                }

                // Add any remaining line
                if (currentLine != null)
                {
                    lines.Add(currentLine);
                }
            }
            return lines.ToArray();
        }

        public class AuthenticationToken
        {
            public string? access_token { get; set; }
            public long? expires_in { get; set; }
            public string? token_type { get; set; }
            public DateTime? token_time { get; } = DateTime.UtcNow;
            public DateTime? expires_at
            {
                get
                {
                    if (expires_in.HasValue && token_time.HasValue)
                    {
                        return token_time?.AddSeconds(expires_in.Value);
                    }
                    return null;
                }
            }
        }
    }
}