using System;
using System.Data;
using Newtonsoft.Json;
using IGDB.Models;
using hasheous_server.Classes.Metadata;
using StackExchange.Redis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Classes
{
    public static class Config
    {
        static ConfigFile _config;

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

        public static ConfigFile.Database DatabaseConfiguration
        {
            get
            {
                return _config.DatabaseConfiguration;
            }
        }

        public static ConfigFile.Redis RedisConfiguration
        {
            get
            {
                return _config.RedisConfiguration;
            }
        }

        public static ConfigFile.Library LibraryConfiguration
        {
            get
            {
                return _config.LibraryConfiguration;
            }
        }

        public static ConfigFile.MetadataAPI MetadataConfiguration
        {
            get
            {
                return _config.MetadataConfiguration;
            }
        }

        public static ConfigFile.IGDB IGDB
        {
            get
            {
                return _config.IGDBConfiguration;
            }
        }

        public static ConfigFile.RetroAchievements RetroAchievements
        {
            get
            {
                return _config.RetroAchievementsConfiguration;
            }
        }

        public static ConfigFile.GiantBomb GiantBomb
        {
            get
            {
                return _config.GiantBombConfiguration;
            }
        }

        public static ConfigFile.SocialAuth SocialAuthConfiguration
        {
            get
            {
                return _config.SocialAuthConfiguration;
            }
        }

        public static string LogPath
        {
            get
            {
                string logPath = Path.Combine(ConfigurationPath, "Logs");
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }
                return logPath;
            }
        }

        public static string LogFilePath
        {
            get
            {
                string logFileExtension = "txt";

                string logPathName = Path.Combine(LogPath, "Server Log " + DateTime.Now.ToUniversalTime().ToString("yyyyMMdd") + "." + logFileExtension);
                return logPathName;
            }
        }

        public static ConfigFile.Logging LoggingConfiguration
        {
            get
            {
                return _config.LoggingConfiguration;
            }
        }

        public static ConfigFile.EmailSMTP EmailSMTPConfiguration
        {
            get
            {
                return _config.EmailSMTPConfiguration;
            }
        }

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

            Console.WriteLine("Using configuration:");
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(_config, Formatting.Indented));
        }

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
                                    AppSettings.Add(SettingName, (T)(object)Convert.ToBoolean(dbResponse.Rows[0]["Value"]));
                                    return (T)(object)Convert.ToBoolean(dbResponse.Rows[0]["Value"]);
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

        public class ConfigFile
        {
            public Database DatabaseConfiguration = new Database();

            public Redis RedisConfiguration = new Redis();

            [JsonIgnore]
            public Library LibraryConfiguration = new Library();

            public MetadataAPI MetadataConfiguration = new MetadataAPI();

            public IGDB IGDBConfiguration = new IGDB();

            public RetroAchievements RetroAchievementsConfiguration = new RetroAchievements();

            public GiantBomb GiantBombConfiguration = new GiantBomb();

            public SocialAuth SocialAuthConfiguration = new SocialAuth();

            public Logging LoggingConfiguration = new Logging();

            public EmailSMTP EmailSMTPConfiguration = new EmailSMTP();

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

                public string HostName = _DefaultHostName;
                public string UserName = _DefaultUserName;
                public string Password = _DefaultPassword;
                public string DatabaseName = "hasheous";
                public int Port = 3306;

                [JsonIgnore]
                public string ConnectionString
                {
                    get
                    {
                        string dbConnString = "server=" + HostName + ";port=" + Port + ";userid=" + UserName + ";password=" + Password + ";database=" + DatabaseName + "";
                        return dbConnString;
                    }
                }

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

            public class Redis
            {
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

            public class Library
            {
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

                public string LibraryUploadDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Upload");
                    }
                }

                public string LibraryMetadataDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Metadata");
                    }
                }

                public string LibraryMetadataDirectory_IGDB
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "IGDB");
                    }
                }

                public string LibraryMetadataDirectory_VIMMSLair
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "VIMMSLair");
                    }
                }

                public string LibraryMetadataDirectory_TheGamesDb
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "TheGamesDb");
                    }
                }

                public string LibraryMetadataDirectory_RetroAchievements
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "RetroAchievements");
                    }
                }

                public string LibraryMetadataDirectory_GiantBomb
                {
                    get
                    {
                        return Path.Combine(LibraryMetadataDirectory, "GiantBomb");
                    }
                }

                public string LibrarySignaturesDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Signatures");
                    }
                }

                public string LibrarySignaturesProcessedDirectory
                {
                    get
                    {
                        return Path.Combine(LibraryRootDirectory, "Signatures - Processed");
                    }
                }

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

                public void InitLibrary()
                {
                    if (!Directory.Exists(LibraryRootDirectory)) { Directory.CreateDirectory(LibraryRootDirectory); }
                    if (!Directory.Exists(LibraryUploadDirectory)) { Directory.CreateDirectory(LibraryUploadDirectory); }
                    if (!Directory.Exists(LibraryMetadataDirectory)) { Directory.CreateDirectory(LibraryMetadataDirectory); }
                    if (!Directory.Exists(LibraryMetadataDirectory_IGDB)) { Directory.CreateDirectory(LibraryMetadataDirectory_IGDB); }
                    if (!Directory.Exists(LibraryMetadataDirectory_VIMMSLair)) { Directory.CreateDirectory(LibraryMetadataDirectory_VIMMSLair); }
                    if (!Directory.Exists(LibraryMetadataDirectory_TheGamesDb)) { Directory.CreateDirectory(LibraryMetadataDirectory_TheGamesDb); }
                    if (!Directory.Exists(LibraryMetadataDirectory_RetroAchievements)) { Directory.CreateDirectory(LibraryMetadataDirectory_RetroAchievements); }
                    if (!Directory.Exists(LibraryMetadataDirectory_GiantBomb)) { Directory.CreateDirectory(LibraryMetadataDirectory_GiantBomb); }
                    if (!Directory.Exists(LibraryTempDirectory)) { Directory.CreateDirectory(LibraryTempDirectory); }
                }
            }

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

                public Communications.MetadataSources Source = _Source;
            }

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

                public string ClientId = _DefaultIGDBClientId;
                public string Secret = _DefaultIGDBSecret;
                public bool UseDumps = _UseDumps;

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

                public string APIKey = _DefaultAPIKey;

                public string BaseURL = "https://www.giantbomb.com/api/";
            }

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

                public string GoogleClientId = _GoogleClientId;
                public string GoogleClientSecret = _GoogleClientSecret;

                public string MicrosoftClientId = _MicrosoftClientId;
                public string MicrosoftClientSecret = _MicrosoftClientSecret;

                [JsonIgnore]
                public bool GoogleAuthEnabled
                {
                    get
                    {
                        return !String.IsNullOrEmpty(GoogleClientId) && !String.IsNullOrEmpty(GoogleClientSecret);
                    }
                }

                [JsonIgnore]
                public bool MicrosoftAuthEnabled
                {
                    get
                    {
                        return !String.IsNullOrEmpty(MicrosoftClientId) && !String.IsNullOrEmpty(MicrosoftClientSecret);
                    }
                }
            }

            public class Logging
            {
                public bool DebugLogging = false;

                // log retention in days
                public int LogRetention = 7;

                public bool AlwaysLogToDisk = false;
                public bool OnlyLogToDisk = false;
            }

            public class EmailSMTP
            {
                public string Host = "";
                public int Port = 587;
                public bool EnableSSL = true;
                public string UserName = "";
                public string Password = "";
                public string SendAs = "";
            }
        }
    }
}
