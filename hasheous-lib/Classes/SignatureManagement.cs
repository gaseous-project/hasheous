using System.Data;
using System.Threading.Tasks;
using hasheous.Classes;
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

        /// <summary>
        /// Gets the raw signature data for a given set of hashes. This is used by the lookup endpoints to get the signature data that is then filtered and returned to the user.
        /// </summary>
        /// <param name="models">A list of hash lookup models containing the hashes to search for.</param>
        /// <returns>A list of raw signature data matching the provided hashes.</returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<Signatures_Games_2>> GetRawSignatures(List<HashLookupModel> models)
        {
            // check the archive observations for the provided hashes
            HashLookupModel? observedHashes = await GetObservedArchiveHashesAsync(models.FirstOrDefault());
            if (observedHashes != null)
            {
                // if the archive is observed, return the hashes from the observations
                models.FirstOrDefault().MD5 = observedHashes.MD5;
                models.FirstOrDefault().SHA1 = observedHashes.SHA1;
                models.FirstOrDefault().SHA256 = observedHashes.SHA256;
                models.FirstOrDefault().CRC = observedHashes.CRC;
            }

            string cacheKey = RedisConnection.GenerateKey("Signature", models);
            // check if the query is cached
            if (Config.RedisConfiguration.Enabled)
            {
                string? cachedData = await hasheous.Classes.RedisConnection.GetDatabase(0).StringGetAsync(cacheKey);
                if (cachedData != null)
                {
                    // if cached data is found, deserialize it and return
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<List<Signatures_Games_2>>(cachedData);
                }
            }

            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            List<string> whereClauses = new List<string>();
            List<string> tokenSelectClauses = new List<string>();
            int modelCount = 0;
            int tokenCount = 0;
            foreach (var model in models)
            {
                if (model.SHA256 != null)
                {
                    if (model.SHA256.Length == 64)
                    {
                        whereClauses.Add("Signatures_Roms.SHA256 = @sha256" + modelCount);
                        tokenSelectClauses.Add("SELECT Signatures_Roms.GameId, 'token" + tokenCount + "' AS HashToken FROM Signatures_Roms WHERE Signatures_Roms.Size > 0 AND Signatures_Roms.SHA256 = @sha256" + modelCount);
                        dbDict.Add("sha256" + modelCount, model.SHA256);
                        tokenCount++;
                    }
                }
                if (model.SHA1 != null)
                {
                    if (model.SHA1.Length == 40)
                    {
                        whereClauses.Add("Signatures_Roms.SHA1 = @sha1" + modelCount);
                        tokenSelectClauses.Add("SELECT Signatures_Roms.GameId, 'token" + tokenCount + "' AS HashToken FROM Signatures_Roms WHERE Signatures_Roms.Size > 0 AND Signatures_Roms.SHA1 = @sha1" + modelCount);
                        dbDict.Add("sha1" + modelCount, model.SHA1);
                        tokenCount++;
                    }
                }
                if (model.MD5 != null)
                {
                    if (model.MD5.Length == 32)
                    {
                        whereClauses.Add("Signatures_Roms.MD5 = @md5" + modelCount);
                        tokenSelectClauses.Add("SELECT Signatures_Roms.GameId, 'token" + tokenCount + "' AS HashToken FROM Signatures_Roms WHERE Signatures_Roms.Size > 0 AND Signatures_Roms.MD5 = @md5" + modelCount);
                        dbDict.Add("md5" + modelCount, model.MD5);
                        tokenCount++;
                    }
                }
                if (model.CRC != null)
                {
                    if (model.CRC.Length == 8)
                    {
                        whereClauses.Add("Signatures_Roms.CRC = @crc" + modelCount);
                        tokenSelectClauses.Add("SELECT Signatures_Roms.GameId, 'token" + tokenCount + "' AS HashToken FROM Signatures_Roms WHERE Signatures_Roms.Size > 0 AND Signatures_Roms.CRC = @crc" + modelCount);
                        dbDict.Add("crc" + modelCount, model.CRC);
                        tokenCount++;
                    }
                }
                modelCount++;
            }

            if (whereClauses.Count > 0 && tokenSelectClauses.Count > 0)
            {
                // lookup the provided hashes
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                dbDict.Add("requiredHashTokenCount", tokenSelectClauses.Count);

                string sql = "SELECT view_Signatures_Games.*, Signatures_Roms.Id AS romid, Signatures_Roms.Name AS romname, Signatures_Roms.Size, Signatures_Roms.CRC, Signatures_Roms.MD5, Signatures_Roms.SHA1, Signatures_Roms.SHA256, Signatures_Roms.Status, Signatures_Roms.DevelopmentStatus, Signatures_Roms.Attributes, Signatures_Roms.RomType, Signatures_Roms.RomTypeMedia, Signatures_Roms.MediaLabel, Signatures_Roms.MetadataSource, Signatures_Roms.Countries, Signatures_Roms.Languages FROM Signatures_Roms INNER JOIN view_Signatures_Games ON Signatures_Roms.GameId = view_Signatures_Games.Id INNER JOIN (SELECT TokenMatches.GameId FROM (" + string.Join(" UNION ALL ", tokenSelectClauses) + ") AS TokenMatches GROUP BY TokenMatches.GameId HAVING COUNT(DISTINCT TokenMatches.HashToken) = @requiredHashTokenCount) AS QualifiedGames ON QualifiedGames.GameId = Signatures_Roms.GameId WHERE Signatures_Roms.Size > 0 AND (" + string.Join(" OR ", whereClauses) + ")";

                DataTable sigDb = await db.ExecuteCMDAsync(sql, dbDict);

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

                // cache the result
                if (Config.RedisConfiguration.Enabled)
                {
                    hasheous.Classes.RedisConnection.GetDatabase(0).StringSet(cacheKey, Newtonsoft.Json.JsonConvert.SerializeObject(GamesList), TimeSpan.FromDays(5));
                }

                return GamesList;
            }
            else
            {
                throw new Exception("Invalid search model");
            }
        }

        private async Task<HashLookupModel?> GetObservedArchiveHashesAsync(HashLookupModel model)
        {
            // check the archive observations for the provided hashes
            // there needs to be at least 5 submissions with the same hashes for the archive to be considered
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = @"
SELECT
    ContentMD5,
    ContentSHA1,
    ContentSHA256,
    ContentCRC32
FROM
    UserArchiveObservations
WHERE
    ArchiveMD5 = @md5
    AND ArchiveSHA1 = @sha1
    AND ArchiveSHA256 = @sha256
GROUP BY
    ContentMD5,
    ContentSHA1,
    ContentSHA256
HAVING
    COUNT(DISTINCT UserId) >= 5;
            ";

            DataTable result = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>
            {
                { "md5", model.MD5 },
                { "sha1", model.SHA1 },
                { "sha256", model.SHA256 },
                { "crc", model.CRC }
            });

            // query should only return one row if the archive is observed and meets the criteria
            if (result.Rows.Count == 1)
            {
                DataRow row = result.Rows[0];
                return new HashLookupModel
                {
                    MD5 = row["ContentMD5"].ToString(),
                    SHA1 = row["ContentSHA1"].ToString(),
                    SHA256 = row["ContentSHA256"].ToString(),
                    CRC = row["ContentCRC32"].ToString()
                };
            }
            else
            {
                // no observed archive found
                return null;
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
                Name = (string)Common.ReturnValueIfNull(sigDbRow["Name"], ""),
                Description = (string)Common.ReturnValueIfNull(sigDbRow["Description"], ""),
                Year = (string)Common.ReturnValueIfNull(sigDbRow["Year"], ""),
                Publisher = (string)Common.ReturnValueIfNull(sigDbRow["Publisher"], ""),
                PublisherId = (long)(int)Common.ReturnValueIfNull(sigDbRow["PublisherId"], ""),
                Demo = (Signatures_Games_2.GameItem.DemoTypes)(int)sigDbRow["Demo"],
                SystemId = (int)Common.ReturnValueIfNull(sigDbRow["PlatformId"], 0),
                System = (string)Common.ReturnValueIfNull(sigDbRow["Platform"], ""),
                SystemVariant = (string)Common.ReturnValueIfNull(sigDbRow["SystemVariant"], ""),
                Video = (string)Common.ReturnValueIfNull(sigDbRow["Video"], ""),
                Country = new Dictionary<string, string>(GetLookup(LookupTypes.Country, (long)sigDbRow["Id"])),
                Countries = new Dictionary<string, string>(GetLookup(LookupTypes.Country, (long)sigDbRow["Id"])),
                Language = new Dictionary<string, string>(GetLookup(LookupTypes.Language, (long)sigDbRow["Id"])),
                Languages = new Dictionary<string, string>(GetLookup(LookupTypes.Language, (long)sigDbRow["Id"])),
                Copyright = (string)Common.ReturnValueIfNull(sigDbRow["Copyright"], ""),
                MetadataSource = (int)sigDbRow["MetadataSource"],
                Category = (string)Common.ReturnValueIfNull(sigDbRow["Category"], "")
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

            var retVal = new Signatures_Games_2.RomItem
            {
                Id = ((long)sigDbRow["romid"]).ToString(),
                Name = (string)sigDbRow["romname"],
                Size = (ulong)(long)sigDbRow["Size"],
                Crc = (string)Common.ReturnValueIfNull((string)sigDbRow["CRC"], ""),
                Md5 = (string)Common.ReturnValueIfNull(((string)sigDbRow["MD5"]).ToLower(), ""),
                Sha1 = (string)Common.ReturnValueIfNull(((string)sigDbRow["SHA1"]).ToLower(), ""),
                Sha256 = ((string)Common.ReturnValueIfNull(sigDbRow["SHA256"], "")).ToLower(),
                Status = ((string)Common.ReturnValueIfNull(sigDbRow["Status"], "")).ToLower(),
                DevelopmentStatus = (string)Common.ReturnValueIfNull((string)sigDbRow["DevelopmentStatus"], ""),
                Attributes = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>((string)Common.ReturnValueIfNull(sigDbRow["Attributes"], "")) ?? new Dictionary<string, object>(),
                RomType = (Signatures_Games_2.RomItem.RomTypes)(int)sigDbRow["RomType"],
                RomTypeMedia = (string)Common.ReturnValueIfNull((string)sigDbRow["RomTypeMedia"], ""),
                MediaLabel = (string)Common.ReturnValueIfNull((string)sigDbRow["MediaLabel"], ""),
                SignatureSource = (Signatures_Games_2.RomItem.SignatureSourceType)(Int32)sigDbRow["MetadataSource"],
                Country = romCountries,
                Language = romLanguages
            };

            // if redump source, add cue sheet if it's available
            switch (retVal.SignatureSource)
            {
                case gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom.SignatureSourceType.Redump:
                    // get the platform name from the database row
                    // we need this to find the cue sheet
                    if (sigDbRow.Table.Columns.Contains("Platform"))
                    {
                        string platformName = (string)Common.ReturnValueIfNull(sigDbRow["Platform"], "");

                        if (!string.IsNullOrEmpty(platformName))
                        {
                            // check if cue sheet is available
                            string cueSheetPath = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_Redump, "cuesheets", platformName, Path.GetFileNameWithoutExtension(retVal.Name) + ".cue");
                            if (System.IO.File.Exists(cueSheetPath))
                            {
                                retVal.Attributes.Add("cuesheet", System.IO.File.ReadAllText(cueSheetPath));
                            }
                        }
                    }
                    break;
                case gaseous_signature_parser.models.RomSignatureObject.RomSignatureObject.Game.Rom.SignatureSourceType.TOSEC:
                    // for TOSEC, check if the datfile has a disc number attribute and if so, add it to the attributes
                    string cuePath = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_TOSEC, "cuesheets", Path.GetFileNameWithoutExtension(retVal.Name) + ".cue");
                    if (File.Exists(cuePath))
                    {
                        retVal.Attributes.Add("cuesheet", File.ReadAllText(cuePath));
                    }
                    break;
            }

            return retVal;
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
                        if (!returnDict.ContainsKey(corrections[(string)row["Code"]].Key))
                        {
                            returnDict.Add(corrections[(string)row["Code"]].Key, corrections[(string)row["Code"]].Value);
                        }
                        continue;
                    }
                }

                if (!returnDict.ContainsKey((string)row["Code"]))
                {
                    returnDict.Add((string)row["Code"], (string)row["Value"]);
                }
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
            string sql = "SELECT Signatures_Roms.`Id` AS romid, Signatures_Roms.`Name` AS romname, Signatures_Roms.*, Signatures_Platforms.Platform FROM Signatures_Roms LEFT JOIN Signatures_Games ON Signatures_Roms.`GameId` = Signatures_Games.`Id` LEFT JOIN Signatures_Platforms ON Signatures_Games.`SystemId` = Signatures_Platforms.`Id` WHERE Signatures_Roms.MD5 = @md5 OR Signatures_Roms.SHA1 = @sha1 OR Signatures_Roms.SHA256 = @sha256 OR Signatures_Roms.CRC = @crc;";

            return BuildRomItem(db.ExecuteCMD(sql, new Dictionary<string, object>{
                { "md5", model.MD5 },
                { "sha1", model.SHA1 },
                { "crc", model.CRC },
                { "sha256", model.SHA256 }
            }).Rows[0]);
        }

        public async Task<hasheous_server.Models.Signatures_Games_2.RomItem> GetRomItemById(long id)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT Signatures_Roms.`Id` AS romid, Signatures_Roms.`Name` AS romname, Signatures_Roms.*, Signatures_Platforms.Platform FROM Signatures_Roms LEFT JOIN Signatures_Games ON Signatures_Roms.`GameId` = Signatures_Games.`Id` LEFT JOIN Signatures_Platforms ON Signatures_Games.`SystemId` = Signatures_Platforms.`Id` WHERE Signatures_Roms.`Id` = @id;";

            var result = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>{
                { "id", id }
            });

            return BuildRomItem(result.Rows[0]);
        }
    }
}