using System.Net;
using System.Reflection;

namespace hasheous_taskrunner.Classes
{
    /// <summary>
    /// Configuration class for the Task Runner application.
    /// </summary>
    public static class Config
    {
        // Configuration
        // Configuration is loaded in the following order - all configuration options are written to the config file when loaded:
        // 1. Default configuration values
        // 2. Configuration file values (overrides default values)
        // 3. Environment variables (overrides configuration file values)
        // 4. Command line arguments (overrides environment variables)
        // Only API key is required for task runner to operate.

        /// <summary>
        /// Loads the configuration from default values, configuration file, environment variables, and command line arguments.
        /// </summary>
        public static void LoadConfiguration()
        {
            // Accessing the Configuration property will load the configuration
            var config = Configuration;
        }

        /// <summary>
        /// Gets the version of the task runner client.
        /// </summary>
        public static Version ClientVersion
        {
            get
            {
                // existing default version to return if assembly lookup fails
                Version defaultVersion = new Version(0, 1, 0, 0);

                try
                {
                    // Prefer the entry assembly for the running executable, fall back to executing assembly
                    Assembly? asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                    if (asm != null)
                    {
                        Version? asmVersion = asm.GetName().Version;
                        if (asmVersion != null)
                        {
                            return asmVersion;
                        }
                    }
                }
                catch
                {
                    // swallow any exceptions and fall back to defaultVersion
                }

                return defaultVersion;
            }
        }

        /// <summary>
        /// The default configuration values.
        /// </summary>
        private static Dictionary<string, string> defaultConfig = new Dictionary<string, string>
        {
            { "HostAddress", "https://hasheous.org/" },
            { "APIKey", "" },
            { "ClientName", Dns.GetHostName() }
        };

        /// <summary>
        /// The base URI path for task worker API endpoints.
        /// </summary>
        public static string BaseUriPath = $"/api/v1/TaskWorker";

        /// <summary>
        /// The required configuration options.
        /// </summary>
        private static List<string> requiredConfigOptions = new List<string>
        {
            "APIKey"
        };

        /// <summary>
        /// The current configuration dictionary.
        /// </summary>
        private static Dictionary<string, string> currentConfig = new Dictionary<string, string>();

        /// <summary>
        /// Gets the current configuration.
        /// </summary>
        public static Dictionary<string, string> Configuration
        {
            get
            {
                if (currentConfig.Count == 0)
                {
                    // load default config
                    currentConfig = new Dictionary<string, string>(defaultConfig);

                    // load config file
                    if (File.Exists(ConfigFilePath))
                    {
                        try
                        {
                            string configJson = File.ReadAllText(ConfigFilePath);
                            var fileConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(configJson);
                            if (fileConfig != null)
                            {
                                Console.WriteLine("Loading configuration from config.json");
                                foreach (var kvp in fileConfig)
                                {
                                    currentConfig[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error loading configuration file: " + ex.Message);
                            Environment.Exit(1);
                        }
                    }

                    // load environment variables
                    foreach (var key in defaultConfig.Keys)
                    {
                        string? envValue = Environment.GetEnvironmentVariable(key);
                        if (!string.IsNullOrEmpty(envValue))
                        {
                            currentConfig[key] = envValue;
                        }
                    }

                    // load command line arguments
                    var args = Environment.GetCommandLineArgs();
                    if (args.Contains("--help") || args.Contains("-h"))
                    {
                        Console.WriteLine("Usage: hasheous-taskrunner [--option value] ...");
                        Console.WriteLine("Available options:");
                        foreach (var kvp in defaultConfig)
                        {
                            Console.WriteLine($"  --{kvp.Key}    (default: {kvp.Value})");
                        }
                        Environment.Exit(0);
                    }

                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i].StartsWith("--"))
                        {
                            string argKey = args[i].Substring(2);
                            if (defaultConfig.ContainsKey(argKey) && i + 1 < args.Length)
                            {
                                currentConfig[argKey] = args[i + 1];
                                i++;
                            }
                        }
                    }

                    // check required config options
                    string missingRequiredOptions = "";
                    foreach (var requiredKey in requiredConfigOptions)
                    {
                        if (!currentConfig.ContainsKey(requiredKey) || string.IsNullOrEmpty(currentConfig[requiredKey]))
                        {
                            missingRequiredOptions += requiredKey + " ";
                        }
                    }
                    if (!string.IsNullOrEmpty(missingRequiredOptions))
                    {
                        throw new Exception("Missing required configuration options: " + missingRequiredOptions);
                    }
                }

                return currentConfig;
            }
        }

        /// <summary>
        /// Internal storage for registration parameters. Should be set before registration, and updated before updating worker info.
        /// </summary>
        public static Dictionary<string, object> RegistrationParameters = new Dictionary<string, object>();

        /// <summary>
        /// The working directory path for the task runner.
        /// </summary>
        public static string workPath
        {
            get
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hasheous-taskrunner");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }

        /// <summary>
        /// The path to the configuration file.
        /// </summary>
        private static string ConfigFilePath { get; } = Path.Combine(workPath, "config.json");

        /// <summary>
        /// The path to the authentication file.
        /// </summary>
        private static string AuthFilePath { get; } = Path.Combine(workPath, "auth.json");

        private static Dictionary<string, string> authData = new Dictionary<string, string>();

        /// <summary>
        /// Retrieves a value from the authentication data store by key.
        /// </summary>
        /// <param name="key">The key to look up in the authentication data.</param>
        /// <returns>The value for the specified key, or an empty string if the key is not present.</returns>
        public static string GetAuthValue(string key)
        {
            if (authData.Count == 0)
            {
                // load auth file
                if (File.Exists(AuthFilePath))
                {
                    try
                    {
                        string authJson = File.ReadAllText(AuthFilePath);
                        var fileAuth = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(authJson);
                        if (fileAuth != null)
                        {
                            authData = fileAuth;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error loading authentication file: " + ex.Message);
                        Environment.Exit(1);
                    }
                }
            }

            if (authData.ContainsKey(key))
            {
                return authData[key];
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// Sets a value in the authentication data store and saves it to the auth file.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void SetAuthValue(string key, string value)
        {
            authData[key] = value;

            // save auth file
            try
            {
                string authJson = System.Text.Json.JsonSerializer.Serialize(authData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(AuthFilePath, authJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving authentication file: " + ex.Message);
                Environment.Exit(1);
            }
        }
    }
}