﻿using System;
using System.Data;
using Newtonsoft.Json;
using IGDB.Models;
using hasheous_server.Classes.Metadata;

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

        private static Dictionary<string, string> AppSettings = new Dictionary<string, string>();

        public static void InitSettings()
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM Settings";

            DataTable dbResponse = db.ExecuteCMD(sql);
            foreach (DataRow dataRow in dbResponse.Rows)
            {
                if (AppSettings.ContainsKey((string)dataRow["Setting"]))
                {
                    AppSettings[(string)dataRow["Setting"]] = (string)dataRow["Value"];
                }
                else
                {
                    AppSettings.Add((string)dataRow["Setting"], (string)dataRow["Value"]);
                }
            }
        }

        public static string ReadSetting(string SettingName, string DefaultValue)
        {
            if (AppSettings.ContainsKey(SettingName))
            {
                return AppSettings[SettingName];
            }
            else
            {
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                string sql = "SELECT * FROM Settings WHERE Setting = @SettingName";
                Dictionary<string, object> dbDict = new Dictionary<string, object>();
                dbDict.Add("SettingName", SettingName);
                dbDict.Add("Value", DefaultValue);

                try
                {
                    DataTable dbResponse = db.ExecuteCMD(sql, dbDict);
                    if (dbResponse.Rows.Count == 0)
                    {
                        // no value with that name stored - respond with the default value
                        SetSetting(SettingName, DefaultValue);
                        return DefaultValue;
                    }
                    else
                    {
                        AppSettings.Add(SettingName, (string)dbResponse.Rows[0][0]);
                        return (string)dbResponse.Rows[0][0];
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
            }
        }

        public static void SetSetting(string SettingName, string Value)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "REPLACE INTO Settings (Setting, Value) VALUES (@SettingName, @Value)";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            dbDict.Add("SettingName", SettingName);
            dbDict.Add("Value", Value);

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
                throw;
            }
        }

        public class ConfigFile
        {
            public Database DatabaseConfiguration = new Database();

            [JsonIgnore]
            public Library LibraryConfiguration = new Library();

            public MetadataAPI MetadataConfiguration = new MetadataAPI();

            public IGDB IGDBConfiguration = new IGDB();

            public RetroAchievements RetroAchievementsConfiguration = new RetroAchievements();

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

                public string ClientId = _DefaultIGDBClientId;
                public string Secret = _DefaultIGDBSecret;
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

            public class Logging
            {
                public bool DebugLogging = false;

                // log retention in days
                public int LogRetention = 7;

                public bool AlwaysLogToDisk = false;
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
