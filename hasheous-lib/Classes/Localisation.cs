using System.Text.Json.Serialization;
using Classes;
using hasheous_server.Classes.Tasks.Clients;
using Newtonsoft.Json;

namespace hasheous_server.Classes
{
    /// <summary>
    /// Handles localisation for the application, including loading localisation files, caching them in memory, and providing methods to retrieve localisation strings for a given language. Localisation files are stored in the library directory and are loaded on demand when a specific language is requested. The class also supports fallback to English localisation if a specific key is not found in the requested language, and it allows for regional variations of languages (e.g. "en-US" for English United States) by merging regional localisation files with the root language file. This class is designed to be efficient by caching loaded localisation entries in memory and only reloading them from disk when necessary, such as when they have been updated since they were last loaded.
    /// </summary>
    public static class Localisation
    {
        private static Dictionary<string, LocalisationEntry> Languages = new Dictionary<string, LocalisationEntry>();

        /// <summary>
        /// Gets all localisation dictionaries for all available languages.
        /// </summary>
        /// <returns>A dictionary of language codes and their corresponding localisation dictionaries.</returns>
        public async static Task<Dictionary<string, Dictionary<string, string>>> GetAllLocalisations()
        {
            PruneLocalisationCache();

            // Load all available languages
            var allLocalisations = new Dictionary<string, Dictionary<string, string>>();
            string[] localisationFiles = Directory.GetFiles(Config.LibraryConfiguration.LibraryLanguageDirectory, "*.json");
            foreach (string localisationFile in localisationFiles)
            {
                var CultureInfo = System.Globalization.CultureInfo.GetCultureInfo(Path.GetFileNameWithoutExtension(localisationFile));
                Dictionary<string, string> localisationStrings = new Dictionary<string, string>
                {
                    { "language_name_in_english", CultureInfo.EnglishName },
                    { "language_name_localised", CultureInfo.NativeName }
                };
                allLocalisations[Path.GetFileNameWithoutExtension(localisationFile)] = localisationStrings;
            }
            return allLocalisations;
        }

        /// <summary>
        /// Gets a localisation dictionary for the requested language using English as a fallback and overriding with requested language values when available.
        /// </summary>
        /// <param name="language">The language code to load, such as "en" or "fr".</param>
        /// <returns>A merged dictionary of localisation keys and values for the requested language.</returns>
        public async static Task<Dictionary<string, string>> GetLanguageStrings(string language)
        {
            PruneLocalisationCache();

            // check cache first to avoid unnecessary file reads and deserialization, which can be expensive operations. If the requested language is already in the cache, we can just return the cached localisation strings. This can help improve performance, especially if there are a lot of localisation keys or if the localisation files are large.
            if (Languages.ContainsKey(language))
            {
                Languages[language].LoadedTime = DateTime.UtcNow; // update the loaded time so that we can keep it in the cache longer, since it's being actively used
                return Languages[language].Strings;
            }

            // if the requested language is not in the cache, we need to load it from the language files. This involves reading the English language file as a base and then overriding it with the requested language file if it exists. If there is a region specified in the language code (e.g. "en-US"), we also need to check for a regional language file and override the values again if it exists. This allows us to have a fallback for any missing localisation keys in the requested language file, and also allows us to only include the localisation keys that are different from the root language file in the regional language files, which can help reduce duplication and make it easier to maintain the localisation files.
            LocalisationEntry localisationEntry = await LoadLanguageFile(language);

            // add the loaded localisation entry to the cache so that it can be quickly retrieved next time without needing to read and deserialize the files again. This is especially beneficial if the same language is requested multiple times, as it can significantly reduce the overhead of loading the localisation data.
            Languages[language] = localisationEntry;

            return localisationEntry.Strings;
        }

        private static void PruneLocalisationCache()
        {
            // prune old languages from the cache to save memory
            List<string> languagesToRemove = new List<string>();
            foreach (var kvp in Languages)
            {
                if (kvp.Value.LoadedTime < DateTime.UtcNow.AddMinutes(-10))
                {
                    languagesToRemove.Add(kvp.Key);
                }
            }
            foreach (string languageToRemove in languagesToRemove)
            {
                Languages.Remove(languageToRemove);
            }
        }

        /// <summary>
        /// Loads a localisation entry for the specified language by reading the corresponding language files from disk, merging them with the English language file as a base, and returning a LocalisationEntry object that contains the merged localisation data. This method also validates that the supplied language code is a recognised ISO culture name before doing any file I/O, so callers get a fast, clear rejection for unknown codes. The localisation files are expected to be in JSON format and located in the library directory, with the English language file being the source of truth for all localisation keys. The method handles both root language files (e.g. "en.json") and regional language files (e.g. "en-US.json"), allowing for regional variations of languages while still providing a fallback to the root language for any missing keys. If the English language file doesn't exist in the library directory, it will be created from the embedded resources to ensure that there is always a base localisation to fall back to.
        /// </summary>
        /// <param name="language">The language code to load, such as "en" or "fr".</param>
        /// <param name="dontLoadEnglishFallback">If set to true, the English fallback will not be loaded.</param>
        /// <returns>A LocalisationEntry object that contains the merged localisation data for the requested language.</returns>
        /// <exception cref="ArgumentException">Thrown when the supplied language code is not a valid ISO culture name.</exception>
        /// <exception cref="Exception">Thrown when the required base English localisation file cannot be loaded.</exception>
        /// <remarks>
        /// The localisation files are expected to be in JSON format and located in the library directory, with the English language file being the source of truth for all localisation keys. The method handles both root language files (e.g. "en.json") and regional language files (e.g. "en-US.json"), allowing for regional variations of languages while still providing a fallback to the root language for any missing keys. If the English language file doesn't exist in the library directory, it will be created from the embedded resources to ensure that there is always a base localisation to fall back to. The method also validates that the supplied language code is a recognised ISO culture name before doing any file I/O, so callers get a fast, clear rejection for unknown codes.
        /// </remarks>
        public static async Task<LocalisationEntry> LoadLanguageFile(string language, bool dontLoadEnglishFallback = false)
        {
            // validate that the supplied code is a recognised ISO culture name (e.g. "en" or "en-AU")
            // before doing any file I/O, so callers get a fast, clear rejection for unknown codes
            try
            {
                System.Globalization.CultureInfo.GetCultureInfo(language);
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                throw new ArgumentException(
                    $"'{language}' is not a valid ISO language code. Examples: 'en', 'en-AU'.",
                    nameof(language));
            }

            // if language is en-US, we just want to load en.json as en.json is already en-US
            if (language == "en-US")
            {
                language = "en";
            }

            // get the root language and region from the language code, e.g. "en-US" => "en" and "US"
            string[] languageParts = language.Split('-');
            string rootLanguage = languageParts[0];
            string region = languageParts.Length > 1 ? languageParts[1] : "";

            // check if the root language file exists in the library directory
            string enRootLanguageFilePath = Path.Combine(Config.LibraryConfiguration.LibraryLanguageDirectory, "en.json");
            string rootLanguageFilePath = Path.Combine(Config.LibraryConfiguration.LibraryLanguageDirectory, $"{rootLanguage}.json");

            LocalisationEntry? localisationEntry = null;

            // the default language is always en, so we need to load the root language file first and then override it with the requested language file if it exists. This allows us to have a fallback for any missing localisation keys in the requested language file, and also allows us to only include the localisation keys that are different from the root language file in the regional language files, which can help reduce duplication and make it easier to maintain the localisation files.
            // the en file is assumed to exist at this point
            if (dontLoadEnglishFallback == false)
            {
                string enJson = await File.ReadAllTextAsync(enRootLanguageFilePath);
                localisationEntry = JsonConvert.DeserializeObject<LocalisationEntry>(enJson);
                if (localisationEntry == null)
                {
                    // something bad happened here, we should have at least the English localisation file, so if we can't load it, we can just return an empty localisation entry
                    throw new Exception("Failed to load required base English localisation file.");
                }

                if (language == "en")
                {
                    // if the requested language is English, we can return the localisation entry now without needing to check for a root language file or regional language file, since English is the base language that all other languages are built on top of
                    return localisationEntry;
                }
                else
                {
                    // set all localisation strings in the English localisation entry as fallback values, so that if they are missing from the requested language file, we can still return the English value as a fallback
                    foreach (var kvp in localisationEntry.LanguageStrings)
                    {
                        kvp.Value.IsFallback = true;
                    }
                }
            }
            else
            {
                localisationEntry = new LocalisationEntry();
            }

            // load the non-english root language file if it exists and replace existing values in localisationEntry
            if (File.Exists(rootLanguageFilePath))
            {
                string json = await File.ReadAllTextAsync(rootLanguageFilePath);
                LocalisationEntry? rootLocalisationEntry = JsonConvert.DeserializeObject<LocalisationEntry>(json);
                if (rootLocalisationEntry != null)
                {
                    // replace the language and region of the localisation entry with the root language and region
                    localisationEntry.Language = rootLocalisationEntry.Language;
                    localisationEntry.Region = rootLocalisationEntry.Region;

                    // merge the root localisation entry with the English localisation entry, with the root values taking precedence over the English values
                    foreach (var kvp in rootLocalisationEntry.LanguageStrings)
                    {
                        localisationEntry.LanguageStrings[kvp.Key] = kvp.Value;
                    }

                    // if the root localisation entry has a friendly name, use it instead of the English friendly name
                    if (!string.IsNullOrEmpty(rootLocalisationEntry.FriendlyName))
                    {
                        localisationEntry.FriendlyName = rootLocalisationEntry.FriendlyName;
                    }

                    // if the root localisation entry has a localised friendly name, use it instead of the English localised friendly name
                    if (!string.IsNullOrEmpty(rootLocalisationEntry.LocalisedFriendlyName))
                    {
                        localisationEntry.LocalisedFriendlyName = rootLocalisationEntry.LocalisedFriendlyName;
                    }
                }
            }
            else
            {
                // since we don't have a root language file for the requested language, we'll simply return the English localisation entry as the fallback, while at the same time queuing a task to generate the root language file for the requested language using AI translation. This way, we can provide a fallback for users while also working towards having a complete localisation for the requested language in the future.
                EnqueueTranslationTask(language);
            }

            // if we just need the root language and there is no region specified, we can return the localisation entry now
            if (string.IsNullOrEmpty(region))
            {
                return localisationEntry;
            }

            // if there is a region specified, we need to check if there is a language file for the specific region and merge it with the root language file
            // regional language files are optional, so if it doesn't exist, we can just return the root language file. They are also additive - their contents will be loaded over the root language file, so they only need to contain the localisation keys that are different from the root language file.
            string regionalLanguageFilePath = Path.Combine(Config.LibraryConfiguration.LibraryLanguageDirectory, $"{rootLanguage}-{region}.json");
            if (File.Exists(regionalLanguageFilePath))
            {
                string json = await File.ReadAllTextAsync(regionalLanguageFilePath);
                LocalisationEntry? regionalLocalisationEntry = JsonConvert.DeserializeObject<LocalisationEntry>(json);

                if (regionalLocalisationEntry != null)
                {
                    // replace the language and region of the localisation entry with the root language and region
                    localisationEntry.Language = regionalLocalisationEntry.Language;
                    localisationEntry.Region = regionalLocalisationEntry.Region;

                    // merge the regional localisation entry with the root localisation entry, with the regional values taking precedence over the root values
                    foreach (var kvp in regionalLocalisationEntry.Strings)
                    {
                        localisationEntry.Strings[kvp.Key] = kvp.Value;
                    }

                    // if the regional localisation entry has a friendly name, use it instead of the root friendly name
                    if (!string.IsNullOrEmpty(regionalLocalisationEntry.FriendlyName))
                    {
                        localisationEntry.FriendlyName = regionalLocalisationEntry.FriendlyName;
                    }

                    // if the regional localisation entry has a localised friendly name, use it instead of the root localised friendly name
                    if (!string.IsNullOrEmpty(regionalLocalisationEntry.LocalisedFriendlyName))
                    {
                        localisationEntry.LocalisedFriendlyName = regionalLocalisationEntry.LocalisedFriendlyName;
                    }
                }
            }
            else
            {
                // since we don't have a locale language file for the requested language, we'll simply return the root language localisation entry as the fallback, while at the same time queuing a task to generate the root language file for the requested language using AI translation. This way, we can provide a fallback for users while also working towards having a complete localisation for the requested language in the future.
                EnqueueTranslationTask(language);
            }

            return localisationEntry;
        }

        /// <summary>
        /// Enqueues a task to generate a localisation file for the specified language by translating the English localisation file using AI translation. This method is called when a requested language file does not exist, and it allows us to work towards having a complete localisation for the requested language in the future while still providing a fallback to the English localisation in the meantime. The task will be added to the task queue with the necessary parameters for the translation, such as the base language and region, so that when the task is processed, it can generate the appropriate localisation file for the requested language.
        /// </summary>
        /// <param name="language">The language and/or locale code for the requested localisation file.</param>
        private static void EnqueueTranslationTask(string language)
        {
            // the task runner will handle the translation and creation of the localisation file for the requested language. it will pull the en language file directly from the host (to ensure it's the latest version), and then use AI translation to create the localisation file for the requested language. This allows us to have a complete localisation for the requested language in the future, while still providing a fallback to the English localisation in the meantime.
            // if language is a locale, the task runner will request the root language file, and translate to that if it's not present before moving to the locale file, so we can just enqueue the task with the requested language and let the task runner handle the rest.
            TaskManagement.EnqueueTask(Models.Tasks.TaskType.AILanguageFileTranslation, language, 10, new List<hasheous_server.Models.Tasks.Capabilities> { Models.Tasks.Capabilities.Internet, Models.Tasks.Capabilities.DiskSpace, Models.Tasks.Capabilities.AI }, new Dictionary<string, string> { });
        }

        /// <summary>
        /// Extracts the English language file from the embedded resources and saves it to the library directory if it doesn't already exist. This method is used to ensure that there is always a base English localisation file available in the library directory, which is necessary for the localisation system to function properly. If the English language file doesn't exist in the library directory, it will be created from the embedded resources, which are included with the application. This allows us to have a fallback localisation available for users even if they haven't added any custom localisation files to the library directory.
        /// </summary>
        public async static Task ExtractEnglishLanguageFile()
        {
            // check if the root language file exists in the library directory
            string enRootLanguageFilePath = Path.Combine(Config.LibraryConfiguration.LibraryLanguageDirectory, "en.json");

            // deserialise the English language file from the embedded resource into a variable for comparison to the on disk version - we're looking for differences to the already saved version
            // if differences are found, we'll kick off tasks to regenerate the changed localisation keys for all languages
            LocalisationEntry? embeddedEnglishLocalisationEntry = null;

            using (Stream? stream = typeof(Localisation).Assembly.GetManifestResourceStream($"hasheous_lib.Support.Localisation.en.json"))
            {
                if (stream != null)
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string json = await reader.ReadToEndAsync();
                        embeddedEnglishLocalisationEntry = JsonConvert.DeserializeObject<LocalisationEntry>(json);
                    }
                }
            }

            if (embeddedEnglishLocalisationEntry == null)
            {
                // something bad happened here, we should have the English localisation file as an embedded resource, so if we can't load it, we can just return and hope that the file exists on disk, since without the English localisation file, the localisation system can't function properly
                throw new Exception("Failed to load required base English localisation from embedded resources.");
            }

            // check if there is an existing English language file on disk
            // - if not, save the embedded English language file to disk so we have a base localisation to work from
            // - if there is, compare it to the embedded English language file and if there are differences, kick off tasks to regenerate the changed localisation keys for all languages, since English is the base localisation that all other localisations are built on top of, so changes to the English localisation may require updates to the other localisations as well
            if (!File.Exists(enRootLanguageFilePath))
            {
                string json = JsonConvert.SerializeObject(embeddedEnglishLocalisationEntry, Formatting.Indented);
                await File.WriteAllTextAsync(enRootLanguageFilePath, json);
            }
            else
            {
                string existingJson = await File.ReadAllTextAsync(enRootLanguageFilePath);
                LocalisationEntry? existingEnglishLocalisationEntry = JsonConvert.DeserializeObject<LocalisationEntry>(existingJson);

                bool changesMade = false;
                if (existingEnglishLocalisationEntry != null)
                {
                    // compare the embedded English localisation entry to the existing English localisation entry on disk, and if there are differences, kick off tasks to regenerate the changed localisation keys for all languages
                    foreach (var kvp in embeddedEnglishLocalisationEntry.Strings)
                    {
                        // update the existing English file
                        if (!existingEnglishLocalisationEntry.LanguageStrings.ContainsKey(kvp.Key) || existingEnglishLocalisationEntry.LanguageStrings[kvp.Key].Value != kvp.Value)
                        {
                            // there is a difference between the embedded English localisation entry and the existing English localisation entry on disk for this localisation key, so we need to update the English localisation file on disk with the new value, and then kick off tasks to regenerate this localisation key for all other languages, since English is the base localisation that all other localisations are built on top of, so changes to the English localisation may require updates to the other localisations as well
                            existingEnglishLocalisationEntry.LanguageStrings[kvp.Key] = new LocalisationEntry.StringItem { Value = kvp.Value, IsAITranslated = false, IsFallback = false, LastUpdated = DateTime.UtcNow };

                            // find all other language files and regenerate this key
                            string[] localisationFiles = Directory.GetFiles(Config.LibraryConfiguration.LibraryLanguageDirectory, "*.json");
                            foreach (string localisationFile in localisationFiles)
                            {
                                string fileName = Path.GetFileNameWithoutExtension(localisationFile);
                                if (fileName == "en")
                                {
                                    continue; // skip the English language file
                                }

                                // this key is missing from the localisation entry, so we need to enqueue a task to generate it using AI translation
                                TaskManagement.EnqueueTask(Models.Tasks.TaskType.AILanguageKeyTranslation, $"{fileName}|{kvp.Key}", 11, new List<hasheous_server.Models.Tasks.Capabilities> { Models.Tasks.Capabilities.Internet, Models.Tasks.Capabilities.DiskSpace, Models.Tasks.Capabilities.AI }, new Dictionary<string, string> { });
                            }

                            changesMade = true;
                        }
                    }
                }

                if (changesMade)
                {
                    // update the English localisation file on disk with the new value
                    string json = JsonConvert.SerializeObject(existingEnglishLocalisationEntry, Formatting.Indented);
                    File.WriteAllText(enRootLanguageFilePath, json);
                }
            }

            // load the language file into memory so it's available for immediate use, and so we can check that it was loaded correctly
            await LoadLanguageFile("en");
        }

        /// <summary>
        /// Checks that all localisation keys in all localisation files have a corresponding entry in the English localisation file, and queues a task to generate any missing localisation keys using AI translation. This method is used to ensure that all localisation files are complete and have the necessary localisation keys, which is important for providing a consistent user experience across different languages.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of checking the localisation keys and queuing translation tasks for any missing keys.</returns>
        public async static Task CheckAllLanguageKeys()
        {
            var englishLanguageFile = await LoadLanguageFile("en");

            // get all localisation files in the library directory
            string[] localisationFiles = Directory.GetFiles(Config.LibraryConfiguration.LibraryLanguageDirectory, "*.json");
            foreach (string localisationFile in localisationFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(localisationFile);
                if (fileName == "en")
                {
                    continue; // skip the English language file
                }

                var localisationEntry = await LoadLanguageFile(fileName, true); // load the localisation file without the English fallback, so we can check which keys are missing compared to the English localisation file

                // check that all keys in the English language file exist in the current localisation entry, and if not, enqueue a task to generate the missing key using AI translation
                foreach (var kvp in englishLanguageFile.Strings)
                {
                    if (!localisationEntry.Strings.ContainsKey(kvp.Key))
                    {
                        // this key is missing from the localisation entry, so we need to enqueue a task to generate it using AI translation
                        TaskManagement.EnqueueTask(Models.Tasks.TaskType.AILanguageKeyTranslation, $"{fileName}|{kvp.Key}", 11, new List<hasheous_server.Models.Tasks.Capabilities> { Models.Tasks.Capabilities.Internet, Models.Tasks.Capabilities.DiskSpace, Models.Tasks.Capabilities.AI }, new Dictionary<string, string> { });
                    }
                }
            }
        }

        /// <summary>
        /// Represents a localisation entry, which contains the language, region, friendly name, localisation strings, and loaded time of a localisation file. This can be used to store the localisation entries in memory and determine when to reload them if they have been updated on disk since they were last loaded.
        /// </summary>
        public class LocalisationEntry
        {
            /// <summary>
            /// The language of the localisation entry, e.g. "en" for English, "fr" for French, etc.
            /// </summary>
            public string Language { get; set; } = "";

            /// <summary>
            /// The region of the localisation entry, e.g. "US" for United States, "FR" for France, etc.
            /// </summary>
            public string Region { get; set; } = "";

            /// <summary>
            /// A friendly name for the localisation entry, e.g. "English (United States)", "French (France)", etc.
            /// </summary>
            public string FriendlyName { get; set; } = "";

            /// <summary>
            /// A localised friendly name for the localisation entry, which is the friendly name translated to the language of the localisation entry. For example, if the friendly name is "French", it will be translated to "Français" for the French localisation entry. This can be used to display the friendly name in the correct language for the user.
            /// </summary>
            public string LocalisedFriendlyName { get; set; } = "";

            /// <summary>
            /// A dictionary of localisation strings, where the key is the localisation key and the value is the localisation string. For example, "welcome_message" => "Welcome to Hasheous!". This can be used to retrieve the appropriate localisation string for a given key when displaying text to the user. The localisation strings are stored as a dictionary of StringItem objects, which contain additional information about each localisation string, such as whether it was translated using AI or if it is a fallback value from the English localisation file. However, for ease of use when retrieving localisation strings, there is also a Strings property that provides a simplified dictionary of just the localisation keys and values.
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore]
            [Newtonsoft.Json.JsonIgnore]
            public Dictionary<string, string> Strings
            {
                get
                {
                    Dictionary<string, string> simpleStrings = new Dictionary<string, string>();
                    foreach (var kvp in LanguageStrings)
                    {
                        simpleStrings[kvp.Key] = kvp.Value.Value;
                    }
                    return simpleStrings;
                }
                set
                {
                    Dictionary<string, StringItem> complexStrings = new Dictionary<string, StringItem>();
                    foreach (var kvp in value)
                    {
                        complexStrings[kvp.Key] = new StringItem { Value = kvp.Value };
                    }
                    LanguageStrings = complexStrings;
                }
            }

            /// <summary>
            /// Legacy JSON input compatibility for older localisation files that use "Strings" instead of "LanguageStrings".
            /// This property is write-only so deserialization can populate values while serialization never emits it.
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("Strings")]
            [Newtonsoft.Json.JsonProperty("Strings")]
            public Dictionary<string, string> LegacyStrings
            {
                set
                {
                    Dictionary<string, StringItem> complexStrings = new Dictionary<string, StringItem>();
                    if (value != null)
                    {
                        foreach (var kvp in value)
                        {
                            complexStrings[kvp.Key] = new StringItem { Value = kvp.Value };
                        }
                    }

                    LanguageStrings = complexStrings;
                }
            }

            /// <summary>
            /// The localisation strings, where the key is the localisation key and the value is the localisation string. For example, "welcome_message" => "Welcome to Hasheous!".
            /// </summary>
            /// <remarks>This property replaces the legacy "Strings" property and allows for additional information about each localisation string to be stored, such as whether it was translated using AI or if it is a fallback value from the English localisation file. The "Strings" property provides a simplified dictionary of just the localisation keys and values for ease of use when retrieving localisation strings, while the "LanguageStrings" property contains the full details for each localisation string.</remarks>
            public Dictionary<string, StringItem> LanguageStrings { get; set; } = new Dictionary<string, StringItem>();

            /// <summary>
            /// Represents a localisation string item, which contains the value of the localisation string, whether it was translated using AI, and whether it is a fallback value from the English localisation file. This can be used to provide additional information about each localisation string, such as whether it was generated using AI translation or if it is a fallback value, which can be useful for debugging and improving the localisation files over time.
            /// </summary>
            public class StringItem
            {
                /// <summary>
                /// The value of the localisation string. For example, "Welcome to Hasheous!".
                /// </summary>
                public string Value { get; set; } = "";

                /// <summary>
                /// Indicates whether the localisation string was translated using AI translation. This can be used to identify which localisation strings were generated using AI and may need to be reviewed for accuracy, as well as to track the progress of AI-generated localisation over time.
                /// </summary>
                public bool IsAITranslated { get; set; } = false;

                /// <summary>
                /// Indicates whether the localisation string is a fallback value from the English localisation file. This can be used to identify which localisation strings are fallback values and may need to be translated for the specific language, as well as to track which localisation strings are still using fallback values over time.
                /// </summary>
                [System.Text.Json.Serialization.JsonIgnore]
                [Newtonsoft.Json.JsonIgnore]
                public bool IsFallback { get; set; } = false;

                /// <summary>
                /// The time when the localisation string was last updated. This can be used to determine when to review and potentially update the localisation string, especially if it was generated using AI translation or if it is a fallback value from the English localisation file, as these may need to be reviewed and updated more frequently to ensure accuracy and relevance for users.
                /// </summary>
                public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
            }

            /// <summary>
            /// A formatted friendly name for the localisation entry, which combines the friendly name and region. For example, if the friendly name is "English" and the region is "United States", the formatted friendly name would be "English (United States)". If the region is empty, it will just return the friendly name. This can be used to display the localisation entry in a user-friendly way, especially when there are multiple localisation entries with the same language but different regions.
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore]
            [Newtonsoft.Json.JsonIgnore]
            public string FormattedFriendlyName
            {
                get
                {
                    if (string.IsNullOrEmpty(Region))
                    {
                        return FriendlyName;
                    }
                    else
                    {
                        return $"{FriendlyName} ({Region})";
                    }
                }
            }

            /// <summary>
            /// A formatted friendly name for the localisation entry that is localised to the language of the localisation entry. This combines the localised friendly name and region. For example, if the localised friendly name is "Français" and the region is "France", the localised formatted friendly name would be "Français (France)". If the region is empty, it will just return the localised friendly name. This can be used to display the localisation entry in a user-friendly way, especially when there are multiple localisation entries with the same language but different regions, and it ensures that the friendly name is displayed in the correct language for the user.
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore]
            [Newtonsoft.Json.JsonIgnore]
            public string LocalisedFormattedFriendlyName
            {
                get
                {
                    if (string.IsNullOrEmpty(Region))
                    {
                        return LocalisedFriendlyName;
                    }
                    else
                    {
                        return $"{LocalisedFriendlyName} ({Region})";
                    }
                }
            }

            /// <summary>
            /// The time when the localisation entry was loaded. This can be used to determine when to reload the localisation entry if it has been updated on disk since it was last loaded.
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore]
            [Newtonsoft.Json.JsonIgnore]
            public DateTime LoadedTime { get; set; } = DateTime.UtcNow;
        }
    }
}