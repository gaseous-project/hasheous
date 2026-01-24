using System;
using System.Data;
using Newtonsoft.Json;
using IGDB.Models;
using hasheous_server.Classes.Metadata;
using StackExchange.Redis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Classes
{
    /// <summary>
    /// Provides application-wide configuration management, including access to database, cache, library, and service settings.
    /// </summary>
    public static class Config
    {
        static ConfigFile _config;

        /// <summary>
        /// Shared database instance using MySQL with the configured connection string.
        /// </summary>
        public static Classes.Database database = null!;

        /// <summary>
        /// Gets the path to the configuration directory under the user's profile.
        /// </summary>
        public static string ConfigurationPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hasheous-server");
            }
        }

        static string ConfigurationFilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hasheous-server", "config.json");
            }
        }

        static string ConfigurationFilePath_Backup
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hasheous-server", "config.json.backup");
            }
        }

        /// <summary>
        /// Gets the database configuration settings.
        /// </summary>
        public static ConfigFile.Database DatabaseConfiguration
        {
            get
            {
                return _config.DatabaseConfiguration;
            }
        }

        /// <summary>
        /// Gets the Redis cache configuration settings.
        /// </summary>
        public static ConfigFile.Redis RedisConfiguration
        {
            get
            {
                return _config.RedisConfiguration;
            }
        }

        /// <summary>
        /// Gets the library configuration settings, including paths for metadata, uploads, and dumps.
        /// </summary>
        public static ConfigFile.Library LibraryConfiguration
        {
            get
            {
                return _config.LibraryConfiguration;
            }
        }

        /// <summary>
        /// Gets the service communication configuration settings, including reporting server URL and API key.
        /// </summary>
        public static ConfigFile.ServiceCommunication ServiceCommunication
        {
            get
            {
                return _config.ServiceConfiguration;
            }
        }

        /// <summary>
        /// Gets the metadata API configuration settings.
        /// </summary>
        public static ConfigFile.MetadataAPI MetadataConfiguration
        {
            get
            {
                return _config.MetadataConfiguration;
            }
        }

        /// <summary>
        /// Gets the IGDB (Internet Game Database) configuration settings.
        /// </summary>
        public static ConfigFile.IGDB IGDB
        {
            get
            {
                return _config.IGDBConfiguration;
            }
        }

        /// <summary>
        /// Gets the RetroAchievements configuration settings.
        /// </summary>
        public static ConfigFile.RetroAchievements RetroAchievements
        {
            get
            {
                return _config.RetroAchievementsConfiguration;
            }
        }

        /// <summary>
        /// Gets the GiantBomb configuration settings.
        /// </summary>
        public static ConfigFile.GiantBomb GiantBomb
        {
            get
            {
                return _config.GiantBombConfiguration;
            }
        }

        /// <summary>
        /// Gets the social authentication configuration settings.
        /// </summary>
        public static ConfigFile.SocialAuth SocialAuthConfiguration
        {
            get
            {
                return _config.SocialAuthConfiguration;
            }
        }

        private static string _logName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "Server Log";
        /// <summary>
        /// Gets or sets the name used for the log file directory and file naming.
        /// </summary>
        public static string LogName
        {
            get
            {
                return _logName;
            }
            set
            {
                _logName = value;
            }
        }

        /// <summary>
        /// Gets the path to the log directory for the current log name, creating it if it does not exist.
        /// </summary>
        public static string LogPath
        {
            get
            {
                string logPath = Path.Combine(ConfigurationPath, "Logs", _logName);
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }
                return logPath;
            }
        }

        /// <summary>
        /// Gets the path to the current log file for the day, using the log directory and a date-based file name.
        /// </summary>
        public static string LogFilePath
        {
            get
            {
                string logFileExtension = "txt";

                string logPathName = Path.Combine(LogPath, DateTime.Now.ToUniversalTime().ToString("yyyyMMdd") + "." + logFileExtension);
                return logPathName;
            }
        }

        /// <summary>
        /// Gets the logging configuration settings.
        /// </summary>
        public static ConfigFile.Logging LoggingConfiguration
        {
            get
            {
                return _config.LoggingConfiguration;
            }
        }

        /// <summary>
        /// Gets the email SMTP configuration settings.
        /// </summary>
        public static ConfigFile.EmailSMTP EmailSMTPConfiguration
        {
            get
            {
                return _config.EmailSMTPConfiguration;
            }
        }

        /// <summary>
        /// Indicates whether the client API key is required for accessing protected endpoints.
        /// Set to false to disable client API key enforcement (e.g., in development mode).
        /// </summary>
        [JsonIgnore]
        public static bool RequireClientAPIKey = true;
        static Config()
        {
            if (_config == null)
            {
                // load the config file
                if (File.Exists(ConfigurationFilePath))
                {
                    string configRaw = File.ReadAllText(ConfigurationFilePath);
                    ConfigFile? _tempConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<ConfigFile>(configRaw);
                    if (_tempConfig != null)
                    {
                        _config = _tempConfig;
                    }
                    else
                    {
                        throw new Exception("There was an error reading the config file: Json returned null");
                    }
                }
                else
                {
                    // no config file!
                    // use defaults and save
                    _config = new ConfigFile();
                    UpdateConfig();
                }
            }
        }

        /// <summary>
        /// Saves any updates to the configuration file, creating a backup of the previous configuration.
        /// </summary>
        public static void UpdateConfig()
        {
            // save any updates to the configuration
            Newtonsoft.Json.JsonSerializerSettings serializerSettings = new Newtonsoft.Json.JsonSerializerSettings
            {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                Formatting = Newtonsoft.Json.Formatting.Indented
            };
            serializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            string configRaw = Newtonsoft.Json.JsonConvert.SerializeObject(_config, serializerSettings);

            if (!Directory.Exists(ConfigurationPath))
            {
                Directory.CreateDirectory(ConfigurationPath);
            }

            if (File.Exists(ConfigurationFilePath_Backup))
            {
                File.Delete(ConfigurationFilePath_Backup);
            }
            if (File.Exists(ConfigurationFilePath))
            {
                File.Move(ConfigurationFilePath, ConfigurationFilePath_Backup);
            }
            File.WriteAllText(ConfigurationFilePath, configRaw);
        }

        private static Dictionary<string, object?> AppSettings = new Dictionary<string, object?>();

        /// <summary>
        /// Reads a setting from the database or cache, returning the value as type <typeparamref name="T"/> if found, or the provided <paramref name="DefaultValue"/> if not.
        /// </summary>
        /// <typeparam name="T">The type of the setting value to retrieve.</typeparam>
        /// <param name="SettingName">The name of the setting to read.</param>
        /// <param name="DefaultValue">The default value to return if the setting is not found.</param>
        /// <returns>The setting value as type <typeparamref name="T"/>.</returns>
        public static T ReadSetting<T>(string SettingName, T DefaultValue)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            try
            {
                if (AppSettings.ContainsKey(SettingName))
                {
                    return (T)AppSettings[SettingName];
                }
                else
                {
                    string sql;
                    Dictionary<string, object> dbDict = new Dictionary<string, object>
                    {
                        { "SettingName", SettingName }
                    };
                    DataTable dbResponse;

                    try
                    {
                        Logging.Log(Logging.LogType.Debug, "Database", "Reading setting '" + SettingName + "'");

                        sql = "SELECT ValueType, Value, ValueDate FROM Settings WHERE Setting = @SettingName";

                        dbResponse = db.ExecuteCMD(sql, dbDict);
                        Type type = typeof(T);
                        if (dbResponse.Rows.Count == 0)
                        {
                            // no value with that name stored - respond with the default value
                            SetSetting<T>(SettingName, DefaultValue);
                            return DefaultValue;
                        }
                        else
                        {
                            if (type.ToString() == "System.DateTime")
                            {
                                AppSettings.Add(SettingName, (T)dbResponse.Rows[0]["ValueDate"]);
                                return (T)dbResponse.Rows[0]["ValueDate"];
                            }
                            else
                            {
                                // cast the value to the requested type
                                if (type.ToString() == "System.String")
                                {
                                    // string value
                                    AppSettings.Add(SettingName, (T)dbResponse.Rows[0]["Value"]);
                                    return (T)dbResponse.Rows[0]["Value"];
                                }
                                else if (type.ToString() == "System.Boolean")
                                {
                                    // boolean value
                                    // convert the value to int first, then to boolean
                                    // this is to handle the case where the value is stored as an int (0 or 1)
                                    // in the database, which is common for boolean values
                                    int intValue = Convert.ToInt32(dbResponse.Rows[0]["Value"].ToString());
                                    if (intValue < 0 || intValue > 1)
                                    {
                                        throw new InvalidCastException("Invalid boolean value stored in database for setting " + SettingName);
                                    }
                                    bool boolValue = Convert.ToBoolean(intValue);

                                    AppSettings.Add(SettingName, (T)(object)boolValue);
                                    return (T)(object)boolValue;
                                }
                                else if (type.ToString() == "System.Int32")
                                {
                                    // int value
                                    AppSettings.Add(SettingName, (T)(object)Convert.ToInt32(dbResponse.Rows[0]["Value"]));
                                    return (T)(object)Convert.ToInt32(dbResponse.Rows[0]["Value"]);
                                }
                                else if (type.ToString() == "System.Int64")
                                {
                                    // long value
                                    AppSettings.Add(SettingName, (T)(object)Convert.ToInt64(dbResponse.Rows[0]["Value"]));
                                    return (T)(object)Convert.ToInt64(dbResponse.Rows[0]["Value"]);
                                }
                                else if (type.ToString() == "System.Single")
                                {
                                    // float value
                                    AppSettings.Add(SettingName, (T)(object)Convert.ToSingle(dbResponse.Rows[0]["Value"]));
                                    return (T)(object)Convert.ToSingle(dbResponse.Rows[0]["Value"]);
                                }
                                else if (type.ToString() == "System.Double")
                                {
                                    // double value
                                    AppSettings.Add(SettingName, (T)(object)Convert.ToDouble(dbResponse.Rows[0]["Value"]));
                                    return (T)(object)Convert.ToDouble(dbResponse.Rows[0]["Value"]);
                                }
                                else if (type.ToString() == "System.Decimal")
                                {
                                    // decimal value
                                    AppSettings.Add(SettingName, (T)(object)Convert.ToDecimal(dbResponse.Rows[0]["Value"]));
                                    return (T)(object)Convert.ToDecimal(dbResponse.Rows[0]["Value"]);
                                }

                                // default case - just return the value as is
                                AppSettings.Add(SettingName, (T)dbResponse.Rows[0]["Value"]);
                                return (T)dbResponse.Rows[0]["Value"];
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Critical, "Database", "Failed reading setting " + SettingName, ex);
                        throw;
                    }
                }
            }
            catch (InvalidCastException castEx)
            {
                Logging.Log(Logging.LogType.Warning, "Settings", "Exception when reading server setting " + SettingName + ". Resetting to default.", castEx);

                // delete broken setting and return the default
                // this error is probably generated during an upgrade
                if (AppSettings.ContainsKey(SettingName))
                {
                    AppSettings.Remove(SettingName);
                }

                string sql = "DELETE FROM Settings WHERE Setting = @SettingName";
                Dictionary<string, object> dbDict = new Dictionary<string, object>
                {
                    { "SettingName", SettingName }
                };

                return DefaultValue;
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, "Settings", "Exception when reading server setting " + SettingName + ".", ex);
                throw;
            }
        }

        /// <summary>
        /// Writes or updates a setting in the database and in-memory cache with the specified value.
        /// </summary>
        /// <typeparam name="T">The type of the setting value to store.</typeparam>
        /// <param name="SettingName">The name of the setting to write.</param>
        /// <param name="Value">The value to store for the setting.</param>
        public static void SetSetting<T>(string SettingName, T Value)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            Dictionary<string, object?> dbDict = new Dictionary<string, object?>
            {
                { "SettingName", SettingName }
            };

            sql = "REPLACE INTO Settings (Setting, ValueType, Value, ValueDate) VALUES (@SettingName, @ValueType, @Value, @ValueDate)";
            Type type = typeof(T);

            switch (type)
            {
                case Type t when t == typeof(DateTime):
                    // value is a DateTime
                    dbDict.Add("ValueType", 1);
                    dbDict.Add("Value", null);
                    dbDict.Add("ValueDate", Value);

                    break;

                case Type t when t == typeof(int) ||
                                  t == typeof(long) ||
                                  t == typeof(float) ||
                                  t == typeof(double) ||
                                  t == typeof(decimal):
                    // value is a number
                    dbDict.Add("Value", Value);
                    dbDict.Add("ValueDate", null);

                    switch (type)
                    {
                        case Type t2 when t2 == typeof(int):
                            dbDict.Add("ValueType", 2);
                            break;

                        case Type t2 when t2 == typeof(long):
                            dbDict.Add("ValueType", 3);
                            break;

                        case Type t2 when t2 == typeof(float):
                            dbDict.Add("ValueType", 4);
                            break;

                        case Type t2 when t2 == typeof(double):
                            dbDict.Add("ValueType", 5);
                            break;

                        case Type t2 when t2 == typeof(decimal):
                            dbDict.Add("ValueType", 6);
                            break;
                    }

                    break;

                case Type t when t == typeof(bool):
                    // value is a boolean
                    dbDict.Add("ValueType", 7);
                    dbDict.Add("Value", Value);
                    dbDict.Add("ValueDate", null);

                    break;

                default:
                    /// value is a string
                    dbDict.Add("ValueType", 0);
                    dbDict.Add("Value", Value);
                    dbDict.Add("ValueDate", null);

                    break;
            }

            Logging.Log(Logging.LogType.Debug, "Database", "Storing setting '" + SettingName + "' to value: '" + Value + "'");
            try
            {
                db.ExecuteCMD(sql, dbDict);

                if (AppSettings.ContainsKey(SettingName))
                {
                    AppSettings[SettingName] = Value;
                }
                else
                {
                    AppSettings.Add(SettingName, Value);
                }
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, "Database", "Failed storing setting" + SettingName, ex);
                throw;
            }
        }

        /// <summary>
        /// Represents the root configuration object containing all application configuration sections.
        /// </summary>
        public class ConfigFile
        {
            /// <summary>
            /// Gets or sets the database configuration settings.
            /// </summary>
            public Database DatabaseConfiguration = new Database();

            /// <summary>
            /// Gets or sets the Redis cache configuration settings.
            /// </summary>
            public Redis RedisConfiguration = new Redis();

            /// <summary>
            /// Gets or sets the library configuration settings, including paths for metadata, uploads, and dumps.
            /// </summary>
            [JsonIgnore]
            public Library LibraryConfiguration = new Library();

            /// <summary>
            /// Gets or sets the service communication configuration settings, including reporting server URL and API key.
            /// </summary>
            public ServiceCommunication ServiceConfiguration = new ServiceCommunication();

            /// <summary>
            /// Gets or sets the metadata API configuration settings.
            /// </summary>
            public MetadataAPI MetadataConfiguration = new MetadataAPI();

            /// <summary>
            /// Gets or sets the IGDB (Internet Game Database) configuration settings.
            /// </summary>
            public IGDB IGDBConfiguration = new IGDB();

            /// <summary>
            /// Gets or sets the RetroAchievements configuration settings.
            /// </summary>
            public RetroAchievements RetroAchievementsConfiguration = new RetroAchievements();

            /// <summary>
            /// Gets or sets the GiantBomb configuration settings.
            /// </summary>
            public GiantBomb GiantBombConfiguration = new GiantBomb();

            /// <summary>
            /// Gets or sets the social authentication configuration settings.
            /// </summary>
            public SocialAuth SocialAuthConfiguration = new SocialAuth();

            /// <summary>
            /// Gets or sets the logging configuration settings.
            /// </summary>
            public Logging LoggingConfiguration = new Logging();

            /// <summary>
            /// Gets or sets the email SMTP configuration settings.
            /// </summary>
            public EmailSMTP EmailSMTPConfiguration = new EmailSMTP();

            /// <summary>
            /// Represents the database configuration settings, including host, user, password, database name, and connection strings.
            /// </summary>
            public class Database
            {
                private static string _DefaultHostName
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("dbhost")))
                        {
                            return Environment.GetEnvironmentVariable("dbhost");
                        }
                        else
                        {
                            return "localhost";
                        }
                    }
                }

                private static string _DefaultUserName
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("dbuser")))
                        {
                            return Environment.GetEnvironmentVariable("dbuser");
                        }
                        else
                        {
                            return "hasheous";
                        }
                    }
                }

                private static string _DefaultPassword
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("dbpass")))
                        {
                            return Environment.GetEnvironmentVariable("dbpass");
                        }
                        else
                        {
                            return "hasheous";
                        }
                    }
                }

                /// <summary>
                /// Gets or sets the database host name.
                /// </summary>
                public string HostName = _DefaultHostName;
                /// <summary>
                /// Gets or sets the database user name.
                /// </summary>
                public string UserName = _DefaultUserName;
                /// <summary>
                /// Gets or sets the database password.
                /// </summary>
                public string Password = _DefaultPassword;
                /// <summary>
                /// Gets or sets the database name.
                /// </summary>
                public string DatabaseName = "hasheous";
                /// <summary>
                /// Gets or sets the database port.
                /// </summary>
                public int Port = 3306;

                /// <summary>
                /// Gets the connection string for the configured MySQL database, including host, port, user, password, and database name.
                /// </summary>
                [JsonIgnore]
                public string ConnectionString
                {
                    get
                    {
                        string dbConnString = "server=" + HostName + ";port=" + Port + ";userid=" + UserName + ";password=" + Password + ";database=" + DatabaseName + "";
                        return dbConnString;
                    }
                }

                /// <summary>
                /// Gets the connection string for the configured MySQL database without specifying the database name, including host, port, user, and password.
                /// </summary>
                [JsonIgnore]
                public string ConnectionStringNoDatabase
                {
                    get
                    {
                        string dbConnString = "server=" + HostName + ";port=" + Port + ";userid=" + UserName + ";password=" + Password + ";";
                        return dbConnString;
                    }
                }
            }

            /// <summary>
            /// Represents the Redis cache configuration settings, including host, port, and enablement.
            /// </summary>
            public class Redis
            {
                /// <summary>
                /// Gets a value indicating whether Redis caching is enabled, based on the 'redisenabled' environment variable.
                /// </summary>
                public bool Enabled
                {
                    get
                    {
                        string? envVar = Environment.GetEnvironmentVariable("redisenabled");
                        if (!String.IsNullOrEmpty(envVar))
                        {
                            return bool.Parse(envVar);
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                /// <summary>
                /// Gets the Redis host name, using the 'redishost' environment variable if set, otherwise defaults to 'localhost'.
                /// </summary>
                public string HostName
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("redishost")))
                        {
                            return Environment.GetEnvironmentVariable("redishost");
                        }
                        else
                        {
                            return "localhost";
                        }
                    }
                }

                /// <summary>
                /// Gets the Redis port, using the 'redisport' environment variable if set, otherwise defaults to 6379.
                /// </summary>
                public int Port
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("redisport")))
                        {
                            return int.Parse(Environment.GetEnvironmentVariable("redisport"));
                        }
                        else
                        {
                            return 6379;
                        }
                    }
                }
            }

            /// <summary>
            /// Represents the library configuration settings, including paths for metadata, uploads, dumps, and signature directories.
            /// </summary>
            public class Library
            {
                /// <summary>
                /// Gets or sets the root directory for the library data storage.
                /// </summary>
                public string LibraryRootDirectory
                {
                    get
                    {
                        return ReadSetting("LibraryRootDirectory", Path.Combine(Config.ConfigurationPath, "Data"));
                    }
                    set
                    {
                        SetSetting("LibraryRootDirectory", value);
                    }
                }

                /// <summary>
                /// Gets the directory path for library uploads.
                /// </summary>
                public string LibraryUploadDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Upload");
                    }
                }

                /// <summary>
                /// Gets the directory path for library metadata.
                /// </summary>
                public string LibraryMetadataDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Metadata");
                    }
                }

                /// <summary>
                /// Gets the directory path for library dumps.
                /// </summary>
                public string LibraryDumpsDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Dumps");
                    }
                }

                /// <summary>
                /// Gets the directory path for library metadata map dumps.
                /// </summary>
                public string LibraryMetadataMapDumpsDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryDumpsDirectory, "MetadataMap");
                    }
                }

                /// <summary>
                /// Gets the directory path for Hasheous-specific metadata.
                /// </summary>
                public string LibraryMetadataDirectory_Hasheous
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "Hasheous");
                    }
                }

                /// <summary>
                /// Gets the directory path for Hasheous-specific images within the metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_HasheousImages
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory_Hasheous, "Images");
                    }
                }

                public string LibraryMetadataBundlesDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "Bundles");
                    }
                }

                /// <summary>
                /// Gets the directory path for IGDB metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_IGDB
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "IGDB");
                    }
                }

                /// <summary>
                /// Gets the directory path for VIMMS Lair metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_VIMMSLair
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "VIMMSLair");
                    }
                }

                /// <summary>
                /// Gets the directory path for TheGamesDb metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_TheGamesDb
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "TheGamesDb");
                    }
                }

                /// <summary>
                /// Gets the directory path for RetroAchievements metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_RetroAchievements
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "RetroAchievements");
                    }
                }

                /// <summary>
                /// Gets the directory path for GiantBomb metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_GiantBomb
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "GiantBomb");
                    }
                }

                /// <summary>
                /// Gets the directory path for Redump metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_Redump
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "Redump");
                    }
                }

                /// <summary>
                /// Gets the directory path for TOSEC metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_TOSEC
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "TOSEC");
                    }
                }

                /// <summary>
                /// Gets the directory path for WHDLoad metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_WHDLoad
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "WHDLoad");
                    }
                }

                /// <summary>
                /// Gets the directory path for MAME Redump metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_MAMERedump
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "MAMERedump");
                    }
                }

                /// <summary>
                /// Gets the directory path for PureDOSDAT metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_PureDOSDAT
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "PureDOSDAT");
                    }
                }

                /// <summary>
                /// Gets the directory path for FBNeo metadata within the library metadata directory.
                /// </summary>
                public string LibraryMetadataDirectory_FBNEO
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "FBNeo");
                    }
                }

                /// <summary>
                /// Gets the directory path for library signatures.
                /// </summary>
                public string LibrarySignaturesDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Signatures");
                    }
                }

                /// <summary>
                /// Gets the directory path for processed library signatures.
                /// </summary>
                public string LibrarySignaturesProcessedDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Signatures - Processed");
                    }
                }

                /// <summary>
                /// Gets the directory path for temporary files within the library root directory.
                /// </summary>
                public string LibraryTempDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Temp");
                    }
                }

                public string LibraryMetadataDirectory_IGDB_Platform(Platform platform)
                {
                    string MetadataPath = Path.Combine(LibraryMetadataDirectory_IGDB, "Platforms", platform.Slug);
                    if (!Directory.Exists(MetadataPath)) { Directory.CreateDirectory(MetadataPath); }
                    return MetadataPath;
                }

                public string LibraryMetadataDirectory_IGDB_Game(Game game)
                {
                    string MetadataPath = Path.Combine(LibraryMetadataDirectory_IGDB, "Games", game.Slug);
                    if (!Directory.Exists(MetadataPath)) { Directory.CreateDirectory(MetadataPath); }
                    return MetadataPath;
                }

                public string LibraryMetadataDirectory_IGDB_Company(Company company)
                {
                    string MetadataPath = Path.Combine(LibraryMetadataDirectory_IGDB, "Companies", company.Slug);
                    if (!Directory.Exists(MetadataPath)) { Directory.CreateDirectory(MetadataPath); }
                    return MetadataPath;
                }

                /// <summary>
                /// Initializes the library directories by creating them if they do not exist.
                /// </summary>
                public void InitLibrary()
                {
                    if (!Directory.Exists(LibraryRootDirectory)) { Directory.CreateDirectory(LibraryRootDirectory); }
                    if (!Directory.Exists(LibraryUploadDirectory)) { Directory.CreateDirectory(LibraryUploadDirectory); }
                    if (!Directory.Exists(LibraryMetadataDirectory)) { Directory.CreateDirectory(LibraryMetadataDirectory); }
                    if (!Directory.Exists(LibraryMetadataDirectory_Hasheous)) { Directory.CreateDirectory(LibraryMetadataDirectory_Hasheous); }
                    if (!Directory.Exists(LibraryMetadataDirectory_HasheousImages)) { Directory.CreateDirectory(LibraryMetadataDirectory_HasheousImages); }
                    if (!Directory.Exists(LibraryMetadataDirectory_IGDB)) { Directory.CreateDirectory(LibraryMetadataDirectory_IGDB); }
                    if (!Directory.Exists(LibraryMetadataDirectory_VIMMSLair)) { Directory.CreateDirectory(LibraryMetadataDirectory_VIMMSLair); }
                    if (!Directory.Exists(LibraryMetadataDirectory_TheGamesDb)) { Directory.CreateDirectory(LibraryMetadataDirectory_TheGamesDb); }
                    if (!Directory.Exists(LibraryMetadataDirectory_RetroAchievements)) { Directory.CreateDirectory(LibraryMetadataDirectory_RetroAchievements); }
                    if (!Directory.Exists(LibraryMetadataDirectory_Redump)) { Directory.CreateDirectory(LibraryMetadataDirectory_Redump); }
                    if (!Directory.Exists(LibraryMetadataDirectory_TOSEC)) { Directory.CreateDirectory(LibraryMetadataDirectory_TOSEC); }
                    if (!Directory.Exists(LibraryMetadataDirectory_WHDLoad)) { Directory.CreateDirectory(LibraryMetadataDirectory_WHDLoad); }
                    if (!Directory.Exists(LibraryMetadataDirectory_MAMERedump)) { Directory.CreateDirectory(LibraryMetadataDirectory_MAMERedump); }
                    if (!Directory.Exists(LibraryMetadataDirectory_FBNEO)) { Directory.CreateDirectory(LibraryMetadataDirectory_FBNEO); }
                    if (!Directory.Exists(LibraryMetadataDirectory_GiantBomb)) { Directory.CreateDirectory(LibraryMetadataDirectory_GiantBomb); }
                    if (!Directory.Exists(LibraryTempDirectory)) { Directory.CreateDirectory(LibraryTempDirectory); }
                }
            }

            /// <summary>
            /// Represents the service communication configuration settings, including reporting server URL and API key.
            /// </summary>
            public class ServiceCommunication
            {
                private static string _ReportingServerUrl
                {
                    get
                    {
                        string? envVar = Environment.GetEnvironmentVariable("reportingserver");
                        if (!String.IsNullOrEmpty(envVar))
                        {
                            return envVar;
                        }
                        else
                        {
                            return "http://localhost:5140";
                        }
                    }
                }

                private static string _APIKey
                {
                    get
                    {
                        string? envVar = Environment.GetEnvironmentVariable("reportingserverapikey");
                        if (!String.IsNullOrEmpty(envVar))
                        {
                            return envVar;
                        }
                        else
                        {
                            // default API key - initial value will be a randomly generated key
                            // use a guid converted to a base64 string
                            return Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=');
                        }
                    }
                }

                /// <summary>
                /// Gets or sets the URL of the reporting server used for service communication.
                /// </summary>
                public string ReportingServerUrl = _ReportingServerUrl;
                /// <summary>
                /// Gets or sets the API key used for authenticating service communication with the reporting server.
                /// </summary>
                public string APIKey = _APIKey;
            }

            /// <summary>
            /// Represents the metadata API configuration settings.
            /// </summary>
            public class MetadataAPI
            {
                private static Communications.MetadataSources _Source
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("metadatasource")))
                        {
                            return (Communications.MetadataSources)Enum.Parse(typeof(Communications.MetadataSources), Environment.GetEnvironmentVariable("metadatasource"));
                        }
                        else
                        {
                            return Communications.MetadataSources.IGDB;
                        }
                    }
                }

                private static int _MetadataBundle_MaxAgeInDays
                {
                    get
                    {
                        string? envVar = Environment.GetEnvironmentVariable("metadatabundlemaxageindays");
                        if (!String.IsNullOrEmpty(envVar))
                        {
                            return int.Parse(envVar);
                        }
                        else
                        {
                            return 7;
                        }
                    }
                }

                private static int _MetadataBundle_MaxStorageInMB
                {
                    get
                    {
                        string? envVar = Environment.GetEnvironmentVariable("metadatabundlemaxstorageinmb");
                        if (!String.IsNullOrEmpty(envVar))
                        {
                            return int.Parse(envVar);
                        }
                        else
                        {
                            return 5000;
                        }
                    }
                }

                private static int _MetadataCache_MaxStorageInMB
                {
                    get
                    {
                        string? envVar = Environment.GetEnvironmentVariable("metadatacachemaxstorageinmb");
                        if (!String.IsNullOrEmpty(envVar))
                        {
                            return int.Parse(envVar);
                        }
                        else
                        {
                            return 10000;
                        }
                    }
                }

                public int MetadataBundle_MaxAgeInDays = _MetadataBundle_MaxAgeInDays;
                public int MetadataBundle_MaxStorageInMB = _MetadataBundle_MaxStorageInMB;
                public int MetadataCache_MaxStorageInMB = _MetadataCache_MaxStorageInMB;

                /// <summary>
                /// Gets or sets the metadata source to use for the API (e.g., IGDB, GiantBomb, etc.).
                /// </summary>
                public Communications.MetadataSources Source = _Source;
            }

            /// <summary>
            /// Represents the IGDB (Internet Game Database) configuration settings.
            /// </summary>
            public class IGDB
            {
                private static string _DefaultIGDBClientId
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("igdbclientid")))
                        {
                            return Environment.GetEnvironmentVariable("igdbclientid");
                        }
                        else
                        {
                            return "";
                        }
                    }
                }

                private static string _DefaultIGDBSecret
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("igdbclientsecret")))
                        {
                            return Environment.GetEnvironmentVariable("igdbclientsecret");
                        }
                        else
                        {
                            return "";
                        }
                    }
                }

                private static bool _UseDumps
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("igdbusedumps")))
                        {
                            return bool.Parse(Environment.GetEnvironmentVariable("igdbusedumps"));
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                /// <summary>
                /// Gets or sets the IGDB client ID used for authenticating API requests.
                /// </summary>
                public string ClientId = _DefaultIGDBClientId;
                /// <summary>
                /// Gets or sets the IGDB client secret used for authenticating API requests.
                /// </summary>
                public string Secret = _DefaultIGDBSecret;
                /// <summary>
                /// Gets or sets a value indicating whether to use IGDB dumps for data retrieval.
                /// </summary>
                public bool UseDumps = _UseDumps;

                /// <summary>
                /// Gets a value indicating whether IGDB dumps are available by checking for the presence of required dump files.
                /// </summary>
                [JsonIgnore]
                public bool DumpsAvailable
                {
                    get
                    {
                        if (
                            File.Exists(Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB, "Dumps", "existing_dumps.json")) &&
                            File.Exists(Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB, "Dumps", "dumps_downloaded.flag"))
                            )
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            /// <summary>
            /// Represents the RetroAchievements configuration settings.
            /// </summary>
            public class RetroAchievements
            {
                private static string _DefaultAPIKey
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("raapikey")))
                        {
                            return Environment.GetEnvironmentVariable("raapikey");
                        }
                        else
                        {
                            return "";
                        }
                    }
                }

                public string APIKey = _DefaultAPIKey;
            }

            /// <summary>
            /// Represents the GiantBomb configuration settings, including API key and base URL.
            /// </summary>
            public class GiantBomb
            {
                private static string _DefaultAPIKey
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("gbapikey")))
                        {
                            return Environment.GetEnvironmentVariable("gbapikey");
                        }
                        else
                        {
                            return "";
                        }
                    }
                }

                /// <summary>
                /// Gets or sets the API key used for authenticating requests to the GiantBomb service.
                /// </summary>
                public string APIKey = _DefaultAPIKey;

                /// <summary>
                /// Gets or sets the base URL for the GiantBomb API.
                /// </summary>
                public string BaseURL = "https://www.giantbomb.com/";
            }

            /// <summary>
            /// Represents the social authentication configuration settings, including Google and Microsoft OAuth credentials.
            /// </summary>
            public class SocialAuth
            {
                private static string _GoogleClientId
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("googleclientid")))
                        {
                            return Environment.GetEnvironmentVariable("googleclientid");
                        }
                        else
                        {
                            return "";
                        }
                    }
                }

                private static string _GoogleClientSecret
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("googleclientsecret")))
                        {
                            return Environment.GetEnvironmentVariable("googleclientsecret");
                        }
                        else
                        {
                            return "";
                        }
                    }
                }

                private static string _MicrosoftClientId
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("microsoftclientid")))
                        {
                            return Environment.GetEnvironmentVariable("microsoftclientid");
                        }
                        else
                        {
                            return "";
                        }
                    }
                }

                private static string _MicrosoftClientSecret
                {
                    get
                    {
                        if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("microsoftclientsecret")))
                        {
                            return Environment.GetEnvironmentVariable("microsoftclientsecret");
                        }
                        else
                        {
                            return "";
                        }
                    }
                }

                /// <summary>
                /// Gets or sets the Google OAuth client ID used for social authentication.
                /// </summary>
                public string GoogleClientId = _GoogleClientId;
                /// <summary>
                /// Gets or sets the Google OAuth client secret used for social authentication.
                /// </summary>
                public string GoogleClientSecret = _GoogleClientSecret;

                /// <summary>
                /// Gets or sets the Microsoft OAuth client ID used for social authentication.
                /// </summary>
                public string MicrosoftClientId = _MicrosoftClientId;
                /// <summary>
                /// Gets or sets the Microsoft OAuth client secret used for social authentication.
                /// </summary>
                public string MicrosoftClientSecret = _MicrosoftClientSecret;

                /// <summary>
                /// Gets a value indicating whether Google authentication is enabled (both client ID and secret are set).
                /// </summary>
                [JsonIgnore]
                public bool GoogleAuthEnabled
                {
                    get
                    {
                        return !String.IsNullOrEmpty(GoogleClientId) && !String.IsNullOrEmpty(GoogleClientSecret);
                    }
                }

                /// <summary>
                /// Gets a value indicating whether Microsoft authentication is enabled (both client ID and secret are set).
                /// </summary>
                [JsonIgnore]
                public bool MicrosoftAuthEnabled
                {
                    get
                    {
                        return !String.IsNullOrEmpty(MicrosoftClientId) && !String.IsNullOrEmpty(MicrosoftClientSecret);
                    }
                }
            }

            /// <summary>
            /// Represents the logging configuration settings, including debug logging, log retention, and disk logging options.
            /// </summary>
            public class Logging
            {
                /// <summary>
                /// Gets or sets a value indicating whether debug logging is enabled.
                /// </summary>
                public bool DebugLogging = false;

                /// <summary>
                /// Gets or sets the log retention period in days.
                /// </summary>
                public int LogRetention = 7;

                /// <summary>
                /// Gets or sets a value indicating whether logs should always be written to disk.
                /// </summary>
                public bool AlwaysLogToDisk = false;

                /// <summary>
                /// Gets or sets a value indicating whether logs should only be written to disk.
                /// </summary>
                public bool OnlyLogToDisk = false;
            }

            /// <summary>
            /// Represents the email SMTP configuration settings, including host, port, SSL, credentials, and sender address.
            /// </summary>
            public class EmailSMTP
            {
                /// <summary>
                /// Gets or sets the SMTP server host.
                /// </summary>
                public string Host = "";
                /// <summary>
                /// Gets or sets the SMTP server port.
                /// </summary>
                public int Port = 587;
                /// <summary>
                /// Gets or sets a value indicating whether SSL is enabled for SMTP.
                /// </summary>
                public bool EnableSSL = true;
                /// <summary>
                /// Gets or sets the SMTP user name.
                /// </summary>
                public string UserName = "";
                /// <summary>
                /// Gets or sets the SMTP password.
                /// </summary>
                public string Password = "";
                /// <summary>
                /// Gets or sets the email address to use as the sender.
                /// </summary>
                public string SendAs = "";
            }
        }
    }
}
