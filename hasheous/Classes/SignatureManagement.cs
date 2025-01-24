using System.Data;
using hasheous_server.Models;
using static Classes.Common;

namespace Classes
{
    public class SignatureManagement
    {
        public class SignatureBadSearchCriteriaException : Exception
        {
            public SignatureBadSearchCriteriaException()
            {
            }

            public SignatureBadSearchCriteriaException(string message)
                : base(message)
            {
            }

            public SignatureBadSearchCriteriaException(string message, Exception inner)
                : base(message, inner)
            {
            }
        }

        public List<Signatures_Games_2> GetRawSignatures(HashLookupModel model)
        {
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            List<string> whereClauses = new List<string>();
            if (model.MD5 != null)
            {
                if (model.MD5.Length == 32)
                {
                    whereClauses.Add("Signatures_Roms.MD5 = @md5");
                    dbDict.Add("md5", model.MD5);
                }
            }
            if (model.SHA1 != null)
            {
                if (model.SHA1.Length == 40)
                {
                    whereClauses.Add("Signatures_Roms.SHA1 = @sha1");
                    dbDict.Add("sha1", model.SHA1);
                }
            }

            if (whereClauses.Count > 0)
            {
                // lookup the provided hashes
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                string sql = "SELECT view_Signatures_Games.*, Signatures_Roms.Id AS romid, Signatures_Roms.Name AS romname, Signatures_Roms.Size, Signatures_Roms.CRC, Signatures_Roms.MD5, Signatures_Roms.SHA1, Signatures_Roms.DevelopmentStatus, Signatures_Roms.Attributes, Signatures_Roms.RomType, Signatures_Roms.RomTypeMedia, Signatures_Roms.MediaLabel, Signatures_Roms.MetadataSource, Signatures_Roms.Countries, Signatures_Roms.Languages FROM Signatures_Roms INNER JOIN view_Signatures_Games ON Signatures_Roms.GameId = view_Signatures_Games.Id WHERE " + string.Join(" OR ", whereClauses);

                DataTable sigDb = db.ExecuteCMD(sql, dbDict);

                List<Signatures_Games_2> GamesList = new List<Signatures_Games_2>();

                foreach (DataRow sigDbRow in sigDb.Rows)
                {
                    Signatures_Games_2.GameItem game = BuildGameItem(sigDbRow);

                    Signatures_Games_2.RomItem rom = BuildRomItem(sigDbRow);

                    Signatures_Games_2 gameItem = new Signatures_Games_2
                    {
                        Game = game,
                        Rom = rom
                    };
                    GamesList.Add(gameItem);
                }
                return GamesList;
            }
            else
            {
                throw new Exception("Invalid search model");
            }
        }

        public object[] SearchSignatures(SignatureSearchModel model)
        {
            // check for errors in search model
            if (model.Ids == null && model.Name == null)
            {
                // no search criteria returned - throw an error
                throw new SignatureBadSearchCriteriaException("No search criteria provided");
            }
            else
            {
                if (model.Name != null)
                {
                    if (model.Name.Length < 3)
                    {
                        // search name too short - throw an error
                        throw new SignatureBadSearchCriteriaException("Name search field must be 3 characters or longer");
                    }
                }
                else if (model.Ids != null)
                {
                    if (model.Ids.Length == 0)
                    {
                        // no ids provided - throw an error
                        throw new SignatureBadSearchCriteriaException("No signature id's provided");
                    }
                }
            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql;
            string? whereNameField = null;
            Dictionary<string, object> dbDict = new Dictionary<string, object>();

            // create where clause for id list if provided
            string? whereClause_Ids = null;
            if (model.Ids != null)
            {
                if (model.Ids.Length > 0)
                {
                    whereClause_Ids = "Id IN ( ";
                    for (int i = 0; i < model.Ids.Length; i++)
                    {
                        if (i > 0)
                        {
                            whereClause_Ids += ", ";
                        }
                        whereClause_Ids += String.Format("@Id{0}", i);
                        dbDict.Add(String.Format("Id{0}", i), model.Ids[i]);
                    }
                    whereClause_Ids += " )";
                }
            }

            string orderBy = "";
            switch (model.SearchType)
            {
                case SignatureSearchModel.SignatureSearchTypes.Publisher:
                    sql = "SELECT * FROM Signatures_Publishers";
                    whereNameField = "Publisher";
                    orderBy = "Publisher";
                    break;

                case SignatureSearchModel.SignatureSearchTypes.Platform:
                    sql = "SELECT * FROM Signatures_Platforms";
                    whereNameField = "Platform";
                    orderBy = "Platform";
                    break;

                case SignatureSearchModel.SignatureSearchTypes.Game:
                    sql = "SELECT Signatures_Games.*, Signatures_Publishers.Publisher, Signatures_Platforms.Id AS PlatformId, Signatures_Platforms.Platform AS Platform FROM Signatures_Games LEFT JOIN Signatures_Publishers ON Signatures_Games.PublisherId = Signatures_Publishers.Id LEFT JOIN Signatures_Platforms ON Signatures_Games.SystemId = Signatures_Platforms.Id";
                    whereNameField = "Name";
                    orderBy = "Signatures_Platforms.Platform, Signatures_Games.`Name`";
                    break;

                case SignatureSearchModel.SignatureSearchTypes.Rom:
                    sql = "SELECT * FROM Signatures_Roms";
                    whereNameField = "Name";
                    orderBy = "Name";
                    break;

                default:
                    throw new SignatureBadSearchCriteriaException("Invalid search type provided");
            }

            // create where clause for name search if provided
            string? whereClause_Name = null;
            if (model.Name != null)
            {
                if (model.Name.Length >= 3)
                {
                    whereClause_Name = "`" + whereNameField + "` LIKE CONCAT('%', @name, '%')";
                    dbDict.Add("name", model.Name);
                }
            }

            // attach where clauses
            sql += " WHERE ";
            if (whereClause_Ids != null)
            {
                sql += whereClause_Ids;
            }

            if (whereClause_Ids != null && whereClause_Name != null)
            {
                sql += " AND ";
            }

            if (whereClause_Name != null)
            {
                sql += whereClause_Name;
            }
            // add order by
            sql += " ORDER BY " + orderBy;

            // limit to 100 rows
            sql += " LIMIT 1000;";

            // execute search
            switch (model.SearchType)
            {
                case SignatureSearchModel.SignatureSearchTypes.Game:
                    List<Signatures_Games_2.GameItem> games = new List<Signatures_Games_2.GameItem>();
                    DataTable gamesData = db.ExecuteCMD(sql, dbDict);
                    foreach (DataRow row in gamesData.Rows)
                    {
                        games.Add(BuildGameItem(row));
                    }
                    return games.ToArray();

                case SignatureSearchModel.SignatureSearchTypes.Rom:
                    List<Signatures_Games_2.RomItem> roms = new List<Signatures_Games_2.RomItem>();
                    DataTable romsData = db.ExecuteCMD(sql, dbDict);
                    foreach (DataRow row in romsData.Rows)
                    {
                        roms.Add(BuildRomItem(row));
                    }
                    return roms.ToArray();

                default:
                    return db.ExecuteCMDDict(sql, dbDict).ToArray();
            }
        }

        public Signatures_Games_2.GameItem BuildGameItem(DataRow sigDbRow)
        {
            return new Signatures_Games_2.GameItem
            {
                Id = ((long)sigDbRow["Id"]).ToString(),
                Name = (string)sigDbRow["Name"],
                Description = (string)sigDbRow["Description"],
                Year = (string)sigDbRow["Year"],
                Publisher = (string)Common.ReturnValueIfNull(sigDbRow["Publisher"], ""),
                PublisherId = (long)(int)Common.ReturnValueIfNull(sigDbRow["PublisherId"], ""),
                Demo = (Signatures_Games_2.GameItem.DemoTypes)(int)sigDbRow["Demo"],
                SystemId = (int)sigDbRow["PlatformId"],
                System = (string)sigDbRow["Platform"],
                SystemVariant = (string)sigDbRow["SystemVariant"],
                Video = (string)sigDbRow["Video"],
                Countries = new Dictionary<string, string>(GetLookup(LookupTypes.Country, (long)sigDbRow["Id"])),
                Languages = new Dictionary<string, string>(GetLookup(LookupTypes.Language, (long)sigDbRow["Id"])),
                Copyright = (string)sigDbRow["Copyright"]
            };
        }

        public Signatures_Games_2.RomItem BuildRomItem(DataRow sigDbRow)
        {
            Dictionary<string, string> romCountries = new Dictionary<string, string>();
            if (sigDbRow["Countries"] != DBNull.Value)
            {
                string strCountries = (string)sigDbRow["Countries"];
                romCountries = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(strCountries);
            }

            Dictionary<string, string> romLanguages = new Dictionary<string, string>();
            if (sigDbRow["Languages"] != DBNull.Value)
            {
                string strLanguages = (string)sigDbRow["Languages"];
                romLanguages = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(strLanguages);
            }

            return new Signatures_Games_2.RomItem
            {
                Id = ((long)sigDbRow["romid"]).ToString(),
                Name = (string)sigDbRow["romname"],
                Size = (ulong)(long)sigDbRow["Size"],
                Crc = (string)sigDbRow["CRC"],
                Md5 = ((string)sigDbRow["MD5"]).ToLower(),
                Sha1 = ((string)sigDbRow["SHA1"]).ToLower(),
                DevelopmentStatus = (string)sigDbRow["DevelopmentStatus"],
                Attributes = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>((string)Common.ReturnValueIfNull(sigDbRow["Attributes"], "")),
                RomType = (Signatures_Games_2.RomItem.RomTypes)(int)sigDbRow["RomType"],
                RomTypeMedia = (string)sigDbRow["RomTypeMedia"],
                MediaLabel = (string)sigDbRow["MediaLabel"],
                SignatureSource = (Signatures_Games_2.RomItem.SignatureSourceType)(Int32)sigDbRow["MetadataSource"],
                Country = romCountries,
                Language = romLanguages
            };
        }

        public Dictionary<string, string> GetLookup(LookupTypes LookupType, long GameId)
        {
            string tableName = "";
            switch (LookupType)
            {
                case LookupTypes.Country:
                    tableName = "Countries";
                    break;

                case LookupTypes.Language:
                    tableName = "Languages";
                    break;

            }

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT " + LookupType.ToString() + ".Code, " + LookupType.ToString() + ".Value FROM Signatures_Games_" + tableName + " JOIN " + LookupType.ToString() + " ON Signatures_Games_" + tableName + "." + LookupType.ToString() + "Id = " + LookupType.ToString() + ".Id WHERE Signatures_Games_" + tableName + ".GameId = @id;";
            Dictionary<string, object> dbDict = new Dictionary<string, object>{
                { "id", GameId }
            };
            DataTable data = db.ExecuteCMD(sql, dbDict);

            Dictionary<string, string> returnDict = new Dictionary<string, string>();
            Dictionary<string, KeyValuePair<string, string>> corrections = GetLookupCorrections(LookupType);
            foreach (DataRow row in data.Rows)
            {
                if (corrections != null)
                {
                    if (corrections.ContainsKey((string)row["Code"]))
                    {
                        returnDict.Add(corrections[(string)row["Code"]].Key, corrections[(string)row["Code"]].Value);
                        continue;
                    }
                }

                returnDict.Add((string)row["Code"], (string)row["Value"]);
            }

            return returnDict;
        }

        public Dictionary<string, KeyValuePair<string, string>> GetLookupCorrections(LookupTypes LookupType)
        {
            switch (LookupType)
            {
                case LookupTypes.Country:
                    Dictionary<string, KeyValuePair<string, string>> countryCodeCorrections = new Dictionary<string, KeyValuePair<string, string>>{
                        { "World", new KeyValuePair<string, string>("World", "Worldwide") },
                        { "UK", new KeyValuePair<string, string>("UK", "United Kingdom") },
                        { "USA", new KeyValuePair<string, string>("US", "United States") },
                        { "Europe", new KeyValuePair<string, string>("EU", "Europe") },
                        { "Argentina", new KeyValuePair<string, string>("AR", "Argentina") },
                        { "Ko", new KeyValuePair<string, string>("KO", "Korea") },
                        { "Da", new KeyValuePair<string, string>("DA", "Denmark") }
                    };
                    return countryCodeCorrections;

                default:
                    return new Dictionary<string, KeyValuePair<string, string>>();
            }
        }

        public hasheous_server.Models.Signatures_Games_2.RomItem GetRomItemByHash(hasheous_server.Models.HashLookupModel model)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT `Id` AS romid, `Name` AS romname, Signatures_Roms.* FROM Signatures_Roms WHERE MD5 = @md5 OR SHA1 = @sha1;";

            return BuildRomItem(db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "md5", model.MD5 },
                { "sha1", model.SHA1 }
            }).Rows[0]);
        }

        public hasheous_server.Models.Signatures_Games_2.RomItem GetRomItemById(long id)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT `Id` AS romid, `Name` AS romname, Signatures_Roms.* FROM Signatures_Roms WHERE Id = @id;";

            return BuildRomItem(db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "id", id }
            }).Rows[0]);
        }
    }
}