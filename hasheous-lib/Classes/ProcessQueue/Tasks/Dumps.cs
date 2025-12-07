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
            string outputPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Content");
            string outputHashesPath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "HashContent");

            // Ensure the output directory exists
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
            // Ensure the output hashes directory exists
            if (!Directory.Exists(outputHashesPath))
            {
                Directory.CreateDirectory(outputHashesPath);
            }

            // Define the path for the zip file
            string zipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataMap.zip");
            string zipHashesFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataHashesMap.zip");

            // Define the path for the platform zip file
            string platformZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platforms");
            string platformTempZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platforms.Temp");

            // Define the path for the platform hashes zip file
            string platformHashesZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platform Hashes");
            string platformHashesTempZipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "Platform Hashes.Temp");

            // Delete any existing temporary platform directory
            if (Directory.Exists(platformTempZipFilePath))
            {
                Directory.Delete(platformTempZipFilePath, true);
            }
            if (Directory.Exists(platformHashesTempZipFilePath))
            {
                Directory.Delete(platformHashesTempZipFilePath, true);
            }

            // Initialise the list of Platforms
            List<string> platforms = new List<string>();

            // initialize the DataObjects class
            DataObjects dataObjects = new DataObjects();

            // step 1: get all game data objects
            // This will fetch all game data objects, which can be paginated.
            for (int pageNumber = 1; ; pageNumber++)
            {
                Logging.Log(Logging.LogType.Information, "Metadata Dump", $"Getting page {pageNumber} of game data objects for dump...");

                var dataObjectsList = await dataObjects.GetDataObjects(DataObjects.DataObjectType.Game, pageNumber, 1000, null, false, false);
                if (dataObjectsList == null || dataObjectsList.Objects.Count == 0)
                {
                    Logging.Log(Logging.LogType.Information, "Metadata Dump", "No more game data objects to process.");
                    break; // No more items to process
                }

                // step 2: dump each game data object
                Logging.Log(Logging.LogType.Information, "Metadata Dump", $"Processing {dataObjectsList.Objects.Count} game data objects from page {pageNumber}...");
                foreach (var dataObjectItem in dataObjectsList.Objects)
                {
                    string platformName = "Unknown Platform";

                    DataObjectItem? dataObject = await dataObjects.GetDataObject(dataObjectItem.Id);

                    if (dataObject == null)
                    {
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
                            DataObjectItem? platformDataObject = await dataObjects.GetDataObject(DataObjects.DataObjectType.Platform, platformItem.Id);
                            if (platformDataObject != null)
                            {
                                string platformFileName = "PlatformMapping.json";
                                string platformFilePath = Path.Combine(platformPath, platformFileName);

                                // serialize the dictionary to JSON and write to file
                                string platformJsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(platformDataObject, new Newtonsoft.Json.JsonSerializerSettings
                                {
                                    Formatting = Newtonsoft.Json.Formatting.Indented,
                                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                    Converters = new List<Newtonsoft.Json.JsonConverter>
                                    {
                                        new Newtonsoft.Json.Converters.StringEnumConverter()
                                    }
                                });
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
                    string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(dataObject, new Newtonsoft.Json.JsonSerializerSettings
                    {
                        Formatting = Newtonsoft.Json.Formatting.Indented,
                        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                        Converters = new List<Newtonsoft.Json.JsonConverter>
                        {
                            new Newtonsoft.Json.Converters.StringEnumConverter()
                        }
                    });

                    // dump hashes data object
                    string platformHashPath = Path.Combine(outputHashesPath, platformName, dataObject.Name + " (" + dataObject.Id + ")");
                    if (!Directory.Exists(platformHashPath))
                    {
                        Directory.CreateDirectory(platformHashPath);
                    }
                    AttributeItem doRoms = dataObject.Attributes?.FirstOrDefault(attr => attr.attributeName == AttributeItem.AttributeName.ROMs);
                    if (doRoms != null)
                    {
                        foreach (var rom in doRoms.Value as List<Signatures_Games_2.RomItem>)
                        {
                            hasheous_server.Models.HashLookupModel hashesDict = new hasheous_server.Models.HashLookupModel();
                            if (rom.Md5 != null)
                            {
                                hashesDict.MD5 = rom.Md5;
                            }
                            if (rom.Sha1 != null)
                            {
                                hashesDict.SHA1 = rom.Sha1;
                            }
                            if (rom.Sha256 != null)
                            {
                                hashesDict.SHA256 = rom.Sha256;
                            }
                            if (rom.Crc != null)
                            {
                                hashesDict.CRC = rom.Crc;
                            }
                            // perform lookup
                            HashLookup hashLookup = new HashLookup(
                                new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString),
                                hashesDict,
                                true,
                                "All"
                            );
                            await hashLookup.PerformLookup();

                            // write the hash lookup result to file - named for the first game and rom id found
                            if (hashLookup.Signatures.Count > 0)
                            {
                                SignatureLookupItem.SignatureResult signature = hashLookup.Signatures.First().Value.First();
                                string romFileName = $"{signature.Game.Id}-{signature.Rom.Id}-HashLookup.json";
                                string romFilePath = Path.Combine(platformHashPath, romFileName);
                                string romJsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(hashLookup, new Newtonsoft.Json.JsonSerializerSettings
                                {
                                    Formatting = Newtonsoft.Json.Formatting.Indented,
                                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                                    Converters = new List<Newtonsoft.Json.JsonConverter>
                                                {
                                                    new Newtonsoft.Json.Converters.StringEnumConverter()
                                                }
                                });
                                await File.WriteAllTextAsync(romFilePath, romJsonContent);
                            }
                        }
                    }
                    await File.WriteAllTextAsync(filePath, jsonContent);
                }
            }

            // step 3: zip the output directory
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Creating main metadata map zip file...");
            System.IO.Compression.ZipFile.CreateFromDirectory(outputPath, zipFilePath);

            if (File.Exists(zipHashesFilePath))
            {
                File.Delete(zipHashesFilePath);
            }
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Creating main metadata hashes map zip file...");
            System.IO.Compression.ZipFile.CreateFromDirectory(outputHashesPath, zipHashesFilePath);

            // step 4: loop the platforms list and create individual zips
            if (Directory.Exists(platformTempZipFilePath))
            {
                Directory.Delete(platformTempZipFilePath, true);
            }
            Directory.CreateDirectory(platformTempZipFilePath);
            if (Directory.Exists(platformHashesTempZipFilePath))
            {
                Directory.Delete(platformHashesTempZipFilePath, true);
            }
            Directory.CreateDirectory(platformHashesTempZipFilePath);
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Creating individual platform zip files...");
            foreach (string platform in platforms)
            {
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
                    System.IO.Compression.ZipFile.CreateFromDirectory(platformSourcePath, platformZipPath);
                }

                // create a zip for the platform hashes
                string platformHashesSourcePath = Path.Combine(outputHashesPath, platform);
                if (Directory.Exists(platformHashesSourcePath))
                {
                    string safePlatformName = platform;
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        safePlatformName = safePlatformName.Replace(c, '_');
                    }
                    string platformHashesZipPath = Path.Combine(platformHashesTempZipFilePath, $"{safePlatformName}.zip");
                    System.IO.Compression.ZipFile.CreateFromDirectory(platformHashesSourcePath, platformHashesZipPath);
                }
            }
            // delete the old platform zip if it exists
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Replacing old platform zip files with new ones...");
            if (Directory.Exists(platformZipFilePath))
            {
                Directory.Delete(platformZipFilePath, true);
            }
            // move the temp platform directory to the final location
            Directory.Move(platformTempZipFilePath, platformZipFilePath);

            // delete the old platform hashes zip if it exists
            if (Directory.Exists(platformHashesZipFilePath))
            {
                Directory.Delete(platformHashesZipFilePath, true);
            }
            // move the temp platform hashes directory to the final location
            Directory.Move(platformHashesTempZipFilePath, platformHashesZipFilePath);

            // clean up the build directory
            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Cleaning up temporary files...");
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }
            if (Directory.Exists(outputHashesPath))
            {
                Directory.Delete(outputHashesPath, true);
            }

            Logging.Log(Logging.LogType.Information, "Metadata Dump", "Metadata dump process completed successfully.");
            return null; // Assuming the method returns void, we return null here.
        }
    }
}