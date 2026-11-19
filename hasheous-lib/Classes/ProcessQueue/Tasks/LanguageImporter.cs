using hasheous.Classes;
using hasheous_server.Classes;
using hasheous_server.Models;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that imports language data into the system.
    /// This task is intended to handle the import of localisation files and update the database accordingly.
    /// </summary>
    public class LanguageImporter : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>();

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync(object? options = null)
        {
            // scan the localisation files directory and import any new or updated language files
            string localisationDirectory = Config.LibraryConfiguration.LibraryLocalisationDirectory;
            if (Directory.Exists(localisationDirectory))
            {
                string[] localisationFiles = Directory.GetFiles(localisationDirectory, "*.json");
                foreach (string file in localisationFiles)
                {
                    string json = await File.ReadAllTextAsync(file);
                    LocalisationItem? item = Newtonsoft.Json.JsonConvert.DeserializeObject<LocalisationItem>(json);
                    if (item != null)
                    {
                        await Localisation.ImportLocalisationFileAsync(item);
                    }
                }
            }

            return null; // Assuming the method returns void, we return null here.
        }
    }
}