using System;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Web;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using Newtonsoft.Json;

namespace Classes
{
    public class JsonPlatformMap
    {
        public static List<PlatformMapItem> PlatformMap = new List<PlatformMapItem>();

        public static void ImportPlatformMap()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("hasheous.Support.PlatformMap.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string rawJson = reader.ReadToEnd();
                PlatformMap = new List<PlatformMapItem>();
                Newtonsoft.Json.JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings{
                    MaxDepth = 64
                };
                PlatformMap = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PlatformMapItem>>(rawJson, jsonSerializerSettings);

                foreach (PlatformMapItem platformMap in PlatformMap)
                {
                    try
                    {
                        Logging.Log(Logging.LogType.Information, "Platform Map", "Checking Platform Map Id " + platformMap.IGDBId + "...");
                        PlatformMapItem mapItem = GetPlatformMap(platformMap.IGDBId);
                        WritePlatformMap(platformMap, true, true);
                    }
                    catch
                    {
                        Logging.Log(Logging.LogType.Information, "Platform Map", "Writing Platform Map Id " + platformMap.IGDBId + " to database.");
                        WritePlatformMap(platformMap, false, false);
                    }
                }
            }
        }

        public static PlatformMapItem GetPlatformMap(long Id)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM PlatformMap WHERE Id = @Id";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            dbDict.Add("Id", Id);
            DataTable data = db.ExecuteCMD(sql, dbDict);

            if (data.Rows.Count > 0)
            {
                PlatformMapItem platformMap = BuildPlatformMapItem(data.Rows[0]);

                return platformMap;
            }
            else
            {
                Exception exception = new Exception("Platform Map Id " + Id + " does not exist.");
                Logging.Log(Logging.LogType.Critical, "Platform Map", "Platform Map Id " + Id + " does not exist.", exception);
                throw exception;
            }
        }

        static PlatformMapItem BuildPlatformMapItem(DataRow row)
        {
            long IGDBId = (long)row["Id"];
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            string sql = "";

            // get platform data
            IGDB.Models.Platform platform = Platforms.GetPlatform(IGDBId);

            // get platform alternate names
            sql = "SELECT * FROM PlatformMap_AlternateNames WHERE Id = @Id ORDER BY Name";
            dbDict.Clear();
            dbDict.Add("Id", IGDBId);
            DataTable altTable = db.ExecuteCMD(sql, dbDict);

            List<string> alternateNames = new List<string>();
            foreach (DataRow altRow in altTable.Rows)
            {
                string altVal = (string)altRow["Name"];
                if (!alternateNames.Contains(altVal, StringComparer.OrdinalIgnoreCase))
                {
                    alternateNames.Add(altVal);
                }
            }
            if (platform.AlternativeName != null)
            {
                if (!alternateNames.Contains(platform.AlternativeName, StringComparer.OrdinalIgnoreCase))
                {
                    alternateNames.Add(platform.AlternativeName);
                }
            }

            // get platform known extensions
            sql = "SELECT * FROM PlatformMap_Extensions WHERE Id = @Id ORDER BY Extension";
            dbDict.Clear();
            dbDict.Add("Id", IGDBId);
            DataTable extTable = db.ExecuteCMD(sql, dbDict);

            List<string> knownExtensions = new List<string>();
            foreach (DataRow extRow in extTable.Rows)
            {
                string extVal = (string)extRow["Extension"];
                if (!knownExtensions.Contains(extVal, StringComparer.OrdinalIgnoreCase))
                {
                    knownExtensions.Add(extVal);
                }
            }

            // get platform unique extensions
            sql = "SELECT * FROM PlatformMap_UniqueExtensions WHERE Id = @Id ORDER BY Extension";
            dbDict.Clear();
            dbDict.Add("Id", IGDBId);
            DataTable uextTable = db.ExecuteCMD(sql, dbDict);

            List<string> uniqueExtensions = new List<string>();
            foreach (DataRow uextRow in uextTable.Rows)
            {
                string uextVal = (string)uextRow["Extension"];
                if (!uniqueExtensions.Contains(uextVal, StringComparer.OrdinalIgnoreCase))
                {
                    uniqueExtensions.Add(uextVal);
                }
            }

            // get platform bios
            sql = "SELECT * FROM PlatformMap_Bios WHERE Id = @Id ORDER BY Filename";
            dbDict.Clear();
            dbDict.Add("Id", IGDBId);
            DataTable biosTable = db.ExecuteCMD(sql, dbDict);

            List<PlatformMapItem.EmulatorBiosItem> bioss = new List<PlatformMapItem.EmulatorBiosItem>();
            foreach (DataRow biosRow in biosTable.Rows)
            {
                PlatformMapItem.EmulatorBiosItem bios = new PlatformMapItem.EmulatorBiosItem
                {
                    filename = (string)Common.ReturnValueIfNull(biosRow["Filename"], ""),
                    description = (string)Common.ReturnValueIfNull(biosRow["Description"], ""),
                    hash = ((string)Common.ReturnValueIfNull(biosRow["Hash"], "")).ToLower()
                };
                bioss.Add(bios);
            }

            // build item
            PlatformMapItem mapItem = new PlatformMapItem();
            mapItem.IGDBId = IGDBId;
            mapItem.IGDBName = platform.Name;
            mapItem.IGDBSlug = platform.Slug;
            mapItem.AlternateNames = alternateNames;
            mapItem.Extensions = new PlatformMapItem.FileExtensions{
                SupportedFileExtensions = knownExtensions,
                UniqueFileExtensions = uniqueExtensions
            };
            mapItem.RetroPieDirectoryName = (string)Common.ReturnValueIfNull(row["RetroPieDirectoryName"], "");
            mapItem.WebEmulator = new PlatformMapItem.WebEmulatorItem{
                Type = (string)Common.ReturnValueIfNull(row["WebEmulator_Type"], ""),
                Core = (string)Common.ReturnValueIfNull(row["WebEmulator_Core"], ""),
                AvailableWebEmulators = Newtonsoft.Json.JsonConvert.DeserializeObject<List<PlatformMapItem.WebEmulatorItem.AvailableWebEmulatorItem>>((string)Common.ReturnValueIfNull(row["AvailableWebEmulators"], "[]"))
            };
            mapItem.Bios = bioss;

            return mapItem;
        }

        public static void WritePlatformMap(PlatformMapItem item, bool Update, bool AllowAvailableEmulatorOverwrite)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            if (Update == false)
            {
                // insert
                sql = "INSERT INTO PlatformMap (Id, RetroPieDirectoryName, WebEmulator_Type, WebEmulator_Core, AvailableWebEmulators) VALUES (@Id, @RetroPieDirectoryName, @WebEmulator_Type, @WebEmulator_Core, @AvailableWebEmulators)";
            }
            else
            {
                // update
                if (AllowAvailableEmulatorOverwrite == true)
                {
                    sql = "UPDATE PlatformMap SET RetroPieDirectoryName=@RetroPieDirectoryName, WebEmulator_Type=@WebEmulator_Type, WebEmulator_Core=@WebEmulator_Core, AvailableWebEmulators=@AvailableWebEmulators WHERE Id = @Id";
                }
                else
                {
                    sql = "UPDATE PlatformMap SET RetroPieDirectoryName=@RetroPieDirectoryName, WebEmulator_Type=@WebEmulator_Type, WebEmulator_Core=@WebEmulator_Core WHERE Id = @Id";
                }
            }
            dbDict.Add("Id", item.IGDBId);
            dbDict.Add("RetroPieDirectoryName", item.RetroPieDirectoryName);
            if (item.WebEmulator != null)
            {
                dbDict.Add("WebEmulator_Type", item.WebEmulator.Type);
                dbDict.Add("WebEmulator_Core", item.WebEmulator.Core);
                dbDict.Add("AvailableWebEmulators", Newtonsoft.Json.JsonConvert.SerializeObject(item.WebEmulator.AvailableWebEmulators));
            }
            else
            {
                dbDict.Add("WebEmulator_Type", "");
                dbDict.Add("WebEmulator_Core", "");
                dbDict.Add("AvailableWebEmulators", "");
            }
            db.ExecuteCMD(sql, dbDict);

            // remove existing items so they can be re-inserted
            sql = "DELETE FROM PlatformMap_AlternateNames WHERE Id = @Id; DELETE FROM PlatformMap_Extensions WHERE Id = @Id; DELETE FROM PlatformMap_UniqueExtensions WHERE Id = @Id; DELETE FROM PlatformMap_Bios WHERE Id = @Id;";
            db.ExecuteCMD(sql, dbDict);

            // insert alternate names
            if (item.AlternateNames != null)
            {
                foreach (string alternateName in item.AlternateNames)
                {
                    if (alternateName != null)
                    {
                        sql = "INSERT INTO PlatformMap_AlternateNames (Id, Name) VALUES (@Id, @Name);";
                        dbDict.Clear();
                        dbDict.Add("Id", item.IGDBId);
                        dbDict.Add("Name", HttpUtility.HtmlDecode(alternateName));
                        db.ExecuteCMD(sql, dbDict);
                    }
                }
            }

            // insert extensions
            if (item.Extensions != null)
            {
                foreach (string extension in item.Extensions.SupportedFileExtensions)
                {
                    sql = "INSERT INTO PlatformMap_Extensions (Id, Extension) VALUES (@Id, @Extension);";
                    dbDict.Clear();
                    dbDict.Add("Id", item.IGDBId);
                    dbDict.Add("Extension", extension.Trim().ToUpper());
                    db.ExecuteCMD(sql, dbDict);
                }

                // delete duplicates
                sql = "DELETE FROM PlatformMap_UniqueExtensions; INSERT INTO PlatformMap_UniqueExtensions SELECT * FROM PlatformMap_Extensions WHERE Extension <> '.ZIP' AND Extension IN (SELECT Extension FROM PlatformMap_Extensions GROUP BY Extension HAVING COUNT(Extension) = 1);";
                db.ExecuteCMD(sql);
            }

            // insert bios
            if (item.Bios != null)
            {
                foreach (PlatformMapItem.EmulatorBiosItem biosItem in item.Bios)
                {
                    sql = "INSERT INTO PlatformMap_Bios (Id, Filename, Description, Hash) VALUES (@Id, @Filename, @Description, @Hash);";
                    dbDict.Clear();
                    dbDict.Add("Id", item.IGDBId);
                    dbDict.Add("Filename", biosItem.filename);
                    dbDict.Add("Description", biosItem.description);
                    dbDict.Add("Hash", biosItem.hash);
                    db.ExecuteCMD(sql, dbDict);
                }
            }
        }
    }
}