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
                        // check if there is a language already imported and compare versions
                        LocalisationItem? existingItem = await Localisation.LoadLocalisationItemFromDatabaseAsync(item.FullCode);
                        if (existingItem != null && existingItem.Version >= item.Version)
                        {
                            // skip import if the existing version is newer or the same
                            continue;
                        }

                        // import the localisation item into the database
                        await Localisation.ImportLocalisationFileAsync(item);

                        // mark the language as available after import
                        await Localisation.SetLanguageAvailablity(true, item.FullCode);
                    }
                }
            }

            return null; // Assuming the method returns void, we return null here.
        }
    }
}