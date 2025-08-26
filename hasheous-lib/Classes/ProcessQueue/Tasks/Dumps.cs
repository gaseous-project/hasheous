using System.Security.Cryptography.Xml;
using hasheous.Classes;
using hasheous_server.Classes;
using hasheous_server.Models;

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

            // Ensure the output directory exists
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Define the path for the zip file
            string zipFilePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataMapDumpsDirectory, "MetadataMap.zip");

            // initialize the DataObjects class
            DataObjects dataObjects = new DataObjects();

            // step 1: get all game data objects
            // This will fetch all game data objects, which can be paginated.
            for (int pageNumber = 1; ; pageNumber++)
            {
                var dataObjectsList = await dataObjects.GetDataObjects(DataObjects.DataObjectType.Game, pageNumber, 1000, null, false, false);
                if (dataObjectsList == null || dataObjectsList.Objects.Count == 0)
                {
                    break; // No more items to process
                }

                // step 2: dump each game data object
                foreach (var dataObjectItem in dataObjectsList.Objects)
                {
                    string platformName = "Unknown Platform";

                    DataObjectItem? dataObject = await dataObjects.GetDataObject(dataObjectItem.Id);

                    if (dataObject == null)
                    {
                        continue; // Skip if the data object is not found
                    }

                    // get the platform for the game
                    if (dataObject.Attributes != null && dataObject.Attributes.Any(attr => attr.attributeName == AttributeItem.AttributeName.Platform))
                    {
                        DataObjectItem platformItem = (DataObjectItem)dataObject.Attributes.First(attr => attr.attributeName == AttributeItem.AttributeName.Platform).Value;

                        if (platformItem != null)
                        {
                            platformName = platformItem.Name;
                        }
                    }

                    string platformPath = Path.Combine(outputPath, platformName);
                    if (!Directory.Exists(platformPath))
                    {
                        Directory.CreateDirectory(platformPath);
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
                    await File.WriteAllTextAsync(filePath, jsonContent);
                }
            }

            // step 3: zip the output directory
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }
            System.IO.Compression.ZipFile.CreateFromDirectory(outputPath, zipFilePath);

            // clean up the build directory
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            return null; // Assuming the method returns void, we return null here.
        }
    }
}