using System.Diagnostics;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Security.Cryptography.Xml;
using hasheous.Classes;
using hasheous_server.Classes;
using hasheous_server.Models;
using HasheousClient.Models.Metadata.TheGamesDb;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that dumps the mapping of hashes to their metadata sources.
    /// </summary>
    public class Dumps : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>();

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Starting metadata dump processes...");

            await DumpMetadataAsync();
            // await DumpMetadataHashAsync();

            // clean up deprecated dump files
            string outputHashesPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "HashContent");
            string zipHashesFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataHashesMap.zip");
            string zipHashesTempFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataHashesMap.Temp.zip");
            string platformHashesZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platform Hashes");
            string platformHashesTempZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platform Hashes.Temp");
            if (Directory.Exists(outputHashesPath))
            {
                Directory.Delete(outputHashesPath, true);
            }
            if (File.Exists(zipHashesFilePath))
            {
                File.Delete(zipHashesFilePath);
            }
            if (File.Exists(zipHashesTempFilePath))
            {
                File.Delete(zipHashesTempFilePath);
            }
            if (Directory.Exists(platformHashesZipFilePath))
            {
                Directory.Delete(platformHashesZipFilePath, true);
            }
            if (Directory.Exists(platformHashesTempZipFilePath))
            {
                Directory.Delete(platformHashesTempZipFilePath, true);
            }

            return null;
        }

        private TimeSpan cacheDuration = TimeSpan.FromDays(3);

        private Newtonsoft.Json.JsonSerializerSettings jsonSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            Converters = new List<Newtonsoft.Json.JsonConverter>
            {
                new Newtonsoft.Json.Converters.StringEnumConverter()
            }
        };

        private async Task<object?> DumpMetadataAsync()
        {
            string outputPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Content");

            // Ensure the output directory exists
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Define the path for the zip file
            string zipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataMap.zip");

            // Define the path for the platform zip file
            string platformZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platforms");
            string platformTempZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platforms.Temp");

            // Delete any existing temporary platform directory
            if (Directory.Exists(platformTempZipFilePath))
            {
                Directory.Delete(platformTempZipFilePath, true);
            }

            // Initialise the list of Platforms
            List<string> platforms = new List<string>();

            // initialize the DataObjects class
            DataObjects dataObjects = new DataObjects();

            // step 1: get all game data objects
            // This will fetch all game data objects, which can be paginated.
            int totalGamesProcessed = 0;
            for (int pageNumber = 1; ; pageNumber++)
            {
                Logging.Log(Logging.LogType.Information, "Metadata Dump", $"Getting page {pageNumber} of game data objects for dump...");

                var dataObjectsList = await dataObjects.GetDataObjects(DataObjects.DataObjectType.Game, pageNumber, 100, null, false, false);
                if (dataObjectsList == null || dataObjectsList.Objects.Count == 0)
                {
                    Logging.Log(Logging.LogType.Information, "Metadata Dump", "No more game data objects to process.");
                    break; // No more items to process
                }

                // step 2: dump each game data object
                Logging.Log(Logging.LogType.Information, "Metadata Dump", $"Processing {dataObjectsList.Objects.Count} game data objects from page {pageNumber}...");
                foreach (var dataObjectItem in dataObjectsList.Objects)
                {
                    totalGamesProcessed++;
                    Logging.Log(Logging.LogType.Information, "Metadata Dump", $"{totalGamesProcessed}/{dataObjectsList.Count}: Processing game (ID: {dataObjectItem.Id})...");
                    Logging.SendReport(Config.LogName, totalGamesProcessed, dataObjectsList.Count, $"Processing {dataObjectItem.Name}...");

                    string platformName = "Unknown Platform";

                    DataObjectItem? dataObject;
                    string cacheKey = RedisConnection.GenerateKey("Dumps", "Game_" + dataObjectItem.Id.ToString());
                    if (RedisConnection.CacheItemExists(cacheKey))
                    {
                        dataObject = RedisConnection.GetCacheItem<DataObjectItem>(cacheKey);
                    }
                    else
                    {
                        dataObject = await dataObjects.GetDataObject(DataObjects.DataObjectType.Game, dataObjectItem.Id);
                        if (dataObject != null)
                        {
                            RedisConnection.SetCacheItem<DataObjectItem>(cacheKey, dataObject, cacheDuration);
                        }
                    }

                    if (dataObject == null)
                    {
                        Logging.Log(Logging.LogType.Information, "Metadata Dump", $"{totalGamesProcessed}/{dataObjectsList.Count}:   Data object with ID {dataObjectItem.Id} not found. Skipping...");

                        continue; // Skip if the data object is not found
                    }

                    // get the platform for the game
                    DataObjectItem? platformItem = null;
                    if (dataObject.Attributes != null && dataObject.Attributes.Any(attr => attr.attributeName == AttributeItem.AttributeName.Platform))
                    {
                        platformItem = (DataObjectItem)dataObject.Attributes.First(attr => attr.attributeName == AttributeItem.AttributeName.Platform).Value;

                        if (platformItem != null)
                        {
                            platformName = platformItem.Name;
                        }
                    }

                    string platformPath = Path.Combine(outputPath, platformName);
                    if (!Directory.Exists(platformPath))
                    {
                        Directory.CreateDirectory(platformPath);

                        // add the platform mapping file
                        if (platformItem != null)
                        {
                            DataObjectItem? platformDataObject;
                            string platformCacheKey = RedisConnection.GenerateKey("Dumps", "Platform_" + platformItem.Id.ToString());
                            if (RedisConnection.CacheItemExists(platformCacheKey))
                            {
                                platformDataObject = RedisConnection.GetCacheItem<DataObjectItem>(platformCacheKey);
                            }
                            else
                            {
                                platformDataObject = await dataObjects.GetDataObject(DataObjects.DataObjectType.Platform, platformItem.Id);
                                if (platformDataObject != null)
                                {
                                    RedisConnection.SetCacheItem<DataObjectItem>(platformCacheKey, platformDataObject, cacheDuration);
                                }
                            }
                            if (platformDataObject != null)
                            {
                                string platformFileName = "PlatformMapping.json";
                                string platformFilePath = Path.Combine(platformPath, platformFileName);

                                // serialize the dictionary to JSON and write to file
                                string platformJsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(platformDataObject, jsonSettings);
                                await File.WriteAllTextAsync(platformFilePath, platformJsonContent);
                            }
                        }
                    }

                    // Add to the list of platforms if not already present
                    if (!platforms.Contains(platformName))
                    {
                        platforms.Add(platformName);
                    }

                    // Ensure the file name is safe for writing to disk
                    string unsafeFileName = $"{dataObject.Name.Trim()} ({dataObject.Id}).json";
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        unsafeFileName = unsafeFileName.Replace(c, '_');
                    }
                    string fileName = unsafeFileName;
                    string filePath = Path.Combine(platformPath, fileName);

                    // serialize the dictionary to JSON and write to file
                    string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(dataObject, jsonSettings);
                    await File.WriteAllTextAsync(filePath, jsonContent);

                    // if counter is a multiple of 10, introduce a short delay
                    if (totalGamesProcessed % 10 == 0)
                    {
                        await Task.Delay(2000); // 2 seconds delay
                    }
                }

                // sleep for a 30 seconds to avoid overwhelming the system
                await Task.Delay(30000);
            }

            Logging.SendReport(Config.LogName, totalGamesProcessed, totalGamesProcessed, "Compressing content.");

            // step 3: zip the output directory
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Creating main metadata map zip file...");
            System.IO.Compression.ZipFile.CreateFromDirectory(outputPath, zipFilePath, System.IO.Compression.CompressionLevel.SmallestSize, false);

            // step 4: loop the platforms list and create individual zips
            if (Directory.Exists(platformTempZipFilePath))
            {
                Directory.Delete(platformTempZipFilePath, true);
            }
            Directory.CreateDirectory(platformTempZipFilePath);

            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Creating individual platform zip files...");
            int platformCounter = 0;
            foreach (string platform in platforms)
            {
                platformCounter++;
                Logging.SendReport(Config.LogName, platformCounter, platforms.Count, $"Creating zip for platform {platform}...");

                // create a zip for the platform
                string platformSourcePath = Path.Combine(outputPath, platform);
                if (Directory.Exists(platformSourcePath))
                {
                    string safePlatformName = platform;
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        safePlatformName = safePlatformName.Replace(c, '_');
                    }
                    string platformZipPath = Path.Combine(platformTempZipFilePath, $"{safePlatformName}.zip");
                    System.IO.Compression.ZipFile.CreateFromDirectory(platformSourcePath, platformZipPath, System.IO.Compression.CompressionLevel.SmallestSize, false);
                    // generate md5 checksum for the zip
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        using (var stream = File.OpenRead(platformZipPath))
                        {
                            var hash = md5.ComputeHash(stream);
                            string md5String = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                            string md5FilePath = platformZipPath + ".md5sum";
                            await File.WriteAllTextAsync(md5FilePath, md5String);
                        }
                    }
                }

                // sleep for 5 seconds to avoid overwhelming the system
                await Task.Delay(5000);
            }
            // delete the old platform zip if it exists
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Replacing old platform zip files with new ones...");
            if (Directory.Exists(platformZipFilePath))
            {
                Directory.Delete(platformZipFilePath, true);
            }
            // move the temp platform directory to the final location
            Directory.Move(platformTempZipFilePath, platformZipFilePath);

            // clean up the build directory
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Cleaning up temporary files...");
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Metadata dump process completed successfully.");
            return null; // Assuming the method returns void, we return null here.
        }

        private async Task<object?> DumpMetadataHashAsync()
        {
            string outputHashesPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "HashContent");

            // Ensure the output hashes directory exists
            if (!Directory.Exists(outputHashesPath))
            {
                Directory.CreateDirectory(outputHashesPath);
            }

            // Define the path for the zip file
            string zipHashesFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataHashesMap.zip");
            string zipHashesTempFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataHashesMap.Temp.zip");

            // Define the path for the platform hashes zip file
            string platformHashesZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platform Hashes");
            string platformHashesTempZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platform Hashes.Temp");

            // Delete any existing temporary platform directory
            if (Directory.Exists(platformHashesTempZipFilePath))
            {
                Directory.Delete(platformHashesTempZipFilePath, true);
            }
            Directory.CreateDirectory(platformHashesTempZipFilePath);
            if (File.Exists(zipHashesTempFilePath))
            {
                File.Delete(zipHashesTempFilePath);
            }
            // Create an empty zip file
            using (var zip = System.IO.Compression.ZipFile.Open(zipHashesTempFilePath, System.IO.Compression.ZipArchiveMode.Create))
            { }

            // Step 1: Get all platform data objects
            DataObjects platformDataObjects = new DataObjects();
            // loop through all platform pages
            for (int platformPageNumber = 1; ; platformPageNumber++)
            {
                var allPlatforms = await platformDataObjects.GetDataObjects(DataObjects.DataObjectType.Platform, platformPageNumber, 100, null, false, false);
                if (allPlatforms == null || allPlatforms.Objects.Count == 0)
                {
                    break; // No more items to process
                }

                // Step 2: For each platform, get all associated game data objects
                foreach (var platform in allPlatforms.Objects)
                {
                    string platformName = platform.Name;
                    // sanitize platform name for file system
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        platformName = platformName.Replace(c, '_');
                    }
                    string platformPath = Path.Combine(outputHashesPath, platformName);

                    // loop through all game pages for the platform
                    bool hasGames = false;
                    for (int gamePageNumber = 1; ; gamePageNumber++)
                    {
                        var platformGames = await platformDataObjects.GetDataObjects(DataObjects.DataObjectType.Game, gamePageNumber, 100, null, false, false, AttributeItem.AttributeName.Platform, platform.Id.ToString());
                        if (platformGames == null || platformGames.Objects.Count == 0)
                        {
                            break; // No more items to process
                        }

                        hasGames = true;

                        // Step 3: Dump each game's hash to metadata source mapping
                        Stopwatch sw = Stopwatch.StartNew();
                        Logging.Log(Logging.LogType.Information, "Metadata Dump", $"Processing {platformGames.Objects.Count} games for platform {platformName} (Page {gamePageNumber})...");
                        foreach (var game in platformGames.Objects)
                        {
                            string gameMetadataPath = Path.Combine(platformPath, game.Name.Trim() + $" ({game.Id})");
                            DataObjectItem? gameObject;
                            string gameCacheKey = RedisConnection.GenerateKey("Dumps", "Game_" + game.Id.ToString());
                            if (RedisConnection.CacheItemExists(gameCacheKey))
                            {
                                gameObject = RedisConnection.GetCacheItem<DataObjectItem>(gameCacheKey);
                            }
                            else
                            {
                                gameObject = await platformDataObjects.GetDataObject(DataObjects.DataObjectType.Game, game.Id);
                                if (gameObject != null)
                                {
                                    RedisConnection.SetCacheItem<DataObjectItem>(gameCacheKey, gameObject, cacheDuration);
                                }
                            }

                            if (gameObject != null && gameObject.Attributes != null)
                            {
                                foreach (var attribute in gameObject.Attributes)
                                {
                                    if (attribute.attributeType == AttributeItem.AttributeType.EmbeddedList && attribute.attributeName == AttributeItem.AttributeName.ROMs)
                                    {
                                        var romList = attribute.Value as List<Signatures_Games_2.RomItem>;
                                        if (romList == null || romList.Count == 0)
                                        {
                                            continue;
                                        }
                                        Stopwatch romSw = Stopwatch.StartNew();
                                        int romCounter = 0;
                                        foreach (var rom in romList)
                                        {
                                            romCounter++;

                                            // check stopwatch - if over 5 minutes, break the loop to avoid long running tasks
                                            if (sw.Elapsed.TotalMinutes > 5)
                                            {
                                                Logging.Log(Logging.LogType.Information, "Metadata Dump", "Time limit reached for this batch, moving to next platform/game...");
                                                break;
                                            }

                                            // check stopwatch - if over 30 seconds, log progress
                                            if (romSw.Elapsed.TotalSeconds > 30)
                                            {
                                                Logging.Log(Logging.LogType.Information, "Metadata Dump", $"  Processed {romCounter}/{romList.Count} ROMs for game {game.Name} ({game.Id})...");
                                                romSw.Restart();
                                            }

                                            // build lookup model
                                            var model = new HashLookupModel
                                            {
                                                MD5 = rom.Md5,
                                                SHA1 = rom.Sha1,
                                                SHA256 = rom.Sha256,
                                                CRC = rom.Crc
                                            };
                                            // fetch the lookup result
                                            HashLookup hashLookup = new HashLookup(Config.database, model, true, "All");
                                            await hashLookup.PerformLookup();
                                            if (hashLookup == null || hashLookup.Signatures == null)
                                            {
                                                continue;
                                            }
                                            // serialise the lookup result to JSON
                                            string romJsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(hashLookup.Signatures, jsonSettings);
                                            // hash the JSON content to create a unique filename
                                            using var md5 = System.Security.Cryptography.MD5.Create();
                                            byte[] hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(romJsonContent));
                                            string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                                            // ensure the game metadata path exists
                                            if (!Directory.Exists(gameMetadataPath))
                                            {
                                                Directory.CreateDirectory(gameMetadataPath);
                                            }
                                            // create the output file
                                            string romOutputPath = Path.Combine(gameMetadataPath, $"{hashString}.json");
                                            await File.WriteAllTextAsync(romOutputPath, romJsonContent);

                                            // sleep for 1 second to avoid overwhelming the system every 10 ROMs
                                            if (romCounter % 10 == 0)
                                            {
                                                await Task.Delay(1000);
                                            }
                                        }
                                        romSw.Stop();

                                        // sleep for 5 seconds to avoid overwhelming the system
                                        await Task.Delay(5000);
                                    }
                                }

                                // sleep for 2 seconds to avoid overwhelming the system
                                await Task.Delay(2000);
                            }
                        }
                        sw.Stop();

                        // sleep for 10 seconds to avoid overwhelming the system
                        await Task.Delay(10000);
                    }

                    // If the platform has no games, skip zipping
                    if (!hasGames)
                    {
                        continue;
                    }

                    // Step 4: Zip the platform hashes
                    string zipPlatformPath = Path.Combine(platformHashesTempZipFilePath, platformName + ".zip");
                    string zipPlatformHashPath = zipPlatformPath + ".zip.md5sum";
                    System.IO.Compression.ZipFile.CreateFromDirectory(platformPath, zipPlatformPath, System.IO.Compression.CompressionLevel.SmallestSize, false);
                    // generate md5 checksum for the zip
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        using (var stream = File.OpenRead(zipPlatformPath))
                        {
                            var hash = md5.ComputeHash(stream);
                            string md5String = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                            string md5FilePath = zipPlatformHashPath;
                            await File.WriteAllTextAsync(md5FilePath, md5String);
                        }
                    }

                    // Step 5: Add the platform directory to the main hashes output zip
                    using (var zip = System.IO.Compression.ZipFile.Open(zipHashesTempFilePath, System.IO.Compression.ZipArchiveMode.Update))
                    {
                        var files = Directory.GetFiles(platformPath, "*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            string entryName = Path.GetRelativePath(outputHashesPath, file);
                            zip.CreateEntryFromFile(file, entryName, System.IO.Compression.CompressionLevel.SmallestSize);
                        }
                    }

                    // Step 6: Move the platform zip to the final location
                    if (!Directory.Exists(platformHashesZipFilePath))
                    {
                        Directory.CreateDirectory(platformHashesZipFilePath);
                    }
                    string finalPlatformZipPath = Path.Combine(platformHashesZipFilePath, platformName + ".zip");
                    if (File.Exists(finalPlatformZipPath))
                    {
                        File.Delete(finalPlatformZipPath);
                    }
                    string finalPlatformHashZipPath = finalPlatformZipPath + ".md5sum";
                    if (File.Exists(finalPlatformHashZipPath))
                    {
                        File.Delete(finalPlatformHashZipPath);
                    }
                    File.Move(zipPlatformPath, finalPlatformZipPath);
                    File.Move(zipPlatformHashPath, finalPlatformHashZipPath);

                    // Clean up the platform path
                    if (Directory.Exists(platformPath))
                    {
                        Directory.Delete(platformPath, true);
                    }
                }

                // sleep for 15 seconds to avoid overwhelming the system
                await Task.Delay(15000);
            }

            // Step 7: Finalise the main hashes zip
            if (File.Exists(zipHashesFilePath))
            {
                File.Delete(zipHashesFilePath);
            }
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Creating main metadata hashes map zip file...");
            File.Move(zipHashesTempFilePath, zipHashesFilePath);

            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Metadata hash dump process completed successfully.");
            return null; // Assuming the method returns void, we return null here.
        }
    }
}