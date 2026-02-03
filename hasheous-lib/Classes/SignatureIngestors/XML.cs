using System;
using System.Data;
using System.IO;
using System.Net;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using Microsoft.CodeAnalysis;
using MySqlConnector;

namespace XML
{
    /// <summary>
    /// Ingestor for importing signature data from XML/DAT files into the database.
    /// Supports multiple signature formats including NoIntro, TOSEC, and MAME.
    /// </summary>
    public class XMLIngestor
    {
        /// <summary>
        /// Imports signature data from XML/DAT files into the database.
        /// </summary>
        /// <param name="SearchPath">The directory path containing XML/DAT files to process.</param>
        /// <param name="XMLType">The type of signature parser to use (e.g., NoIntro, TOSEC, MAME).</param>
        public async Task Import(string SearchPath, gaseous_signature_parser.parser.SignatureParser XMLType)
        {
            // connect to database
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            string? XMLDBSearchPath = null;
            if (XMLType == gaseous_signature_parser.parser.SignatureParser.NoIntro)
            {
                XMLDBSearchPath = Path.Combine(SearchPath, "DB");
                SearchPath = Path.Combine(SearchPath, "DAT");
            }

            // process provided files
            if (!Directory.Exists(SearchPath))
            {
                Directory.CreateDirectory(SearchPath);
            }

            string[] PathContents = Directory.GetFiles(SearchPath);
            Array.Sort(PathContents);

            string[]? DBPathContents = null;
            if (XMLDBSearchPath != null)
            {
                if (!Directory.Exists(XMLDBSearchPath))
                {
                    Directory.CreateDirectory(XMLDBSearchPath);
                }

                DBPathContents = Directory.GetFiles(XMLDBSearchPath);
            }

            DateTime now = DateTime.UtcNow;

            // process dat files
            for (UInt16 i = 0; i < PathContents.Length; ++i)
            {
                Logging.Log(Logging.LogType.Information, "Signature Ingest", "(" + (i + 1) + " / " + PathContents.Length + ") Processing " + XMLType.ToString() + " DAT file: " + PathContents[i]);
                Logging.SendReport(Config.LogName, (i + 1), PathContents.Length, "Processing " + XMLType.ToString() + " DAT file: " + Path.GetFileName(PathContents[i]));

                await ProcessDatFile(PathContents[i], XMLDBSearchPath, DBPathContents, XMLType, db, now);
            }

            // prune old sources
            await PruneOldSources(XMLType, db, now);
        }

        private async Task ProcessDatFile(string XMLFile, string? XMLDBSearchPath, string[]? DBPathContents, gaseous_signature_parser.parser.SignatureParser XMLType, Database db, DateTime now)
        {
            string sql = "";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            System.Data.DataTable sigDB;

            string? DBFile = null;
            if (XMLDBSearchPath != null)
            {
                switch (XMLType)
                {
                    case gaseous_signature_parser.parser.SignatureParser.NoIntro:
                        if (DBPathContents == null)
                        {
                            break;
                        }
                        for (UInt16 x = 0; x < DBPathContents.Length; x++)
                        {
                            string tempDBFileName = Path.GetFileNameWithoutExtension(DBPathContents[x].Replace(" (DB Export)", ""));
                            if (tempDBFileName == Path.GetFileNameWithoutExtension(XMLFile))
                            {
                                DBFile = DBPathContents[x];
                                Logging.Log(Logging.LogType.Information, "Signature Ingest", "Using DB file: " + DBFile);
                                break;
                            }
                        }
                        break;
                }
            }

            try
            {
                // start parsing file
                gaseous_signature_parser.parser Parser = new gaseous_signature_parser.parser();
                RomSignatureObject Object = Parser.ParseSignatureDAT(XMLFile, DBFile, XMLType);

                // store in database
                string[] flipNameAndDescription = {
                            "MAMEArcade",
                            "MAMEMess"
                        };

                // store source object
                bool processGames = false;
                if (Object.SourceMd5 != null)
                {
                    int sourceId = 0;

                    sql = "SELECT * FROM Signatures_Sources WHERE `SourceMD5`=@sourcemd5";
                    dbDict = new Dictionary<string, object>
                    {
                        { "name", Common.ReturnValueIfNull(Object.Name, "") },
                        { "description", Common.ReturnValueIfNull(Object.Description, "") },
                        { "category", Common.ReturnValueIfNull(Object.Category, "") },
                        { "version", Common.ReturnValueIfNull(Object.Version, "") },
                        { "author", Common.ReturnValueIfNull(Object.Author, "") },
                        { "email", Common.ReturnValueIfNull(Object.Email, "") },
                        { "homepage", Common.ReturnValueIfNull(Object.Homepage, "") }
                    };
                    if (Object.Url == null)
                    {
                        dbDict.Add("uri", "");
                    }
                    else
                    {
                        dbDict.Add("uri", Common.ReturnValueIfNull(Object.Url.ToString(), ""));
                    }
                    dbDict.Add("sourcetype", Common.ReturnValueIfNull(Object.SourceType, ""));
                    dbDict.Add("processedat", now);
                    dbDict.Add("sourcemd5", Object.SourceMd5);
                    dbDict.Add("sourcesha1", Object.SourceSHA1);

                    sigDB = await db.ExecuteCMDAsync(sql, dbDict);
                    if (sigDB.Rows.Count == 0)
                    {
                        // entry not present, insert it
                        sql = "INSERT INTO Signatures_Sources (`Name`, `Description`, `Category`, `Version`, `Author`, `Email`, `Homepage`, `Url`, `SourceType`, `processed_at`, `SourceMD5`, `SourceSHA1`) VALUES (@name, @description, @category, @version, @author, @email, @homepage, @uri, @sourcetype, @processedat, @sourcemd5, @sourcesha1); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";

                        sigDB = await db.ExecuteCMDAsync(sql, dbDict);

                        sourceId = Convert.ToInt32(sigDB.Rows[0][0]);

                        processGames = true;
                    }
                    else
                    {
                        // entry present, update processed date
                        sql = "UPDATE Signatures_Sources SET processed_at=@processedat WHERE `SourceMD5`=@sourcemd5;";
                        await db.ExecuteCMDAsync(sql, dbDict);

                        sourceId = Convert.ToInt32(sigDB.Rows[0]["Id"]);
                    }

                    for (int x = 0; x < Object.Games.Count; ++x)
                    {
                        RomSignatureObject.Game gameObject = Object.Games[x];

                        // set up game dictionary
                        dbDict = new Dictionary<string, object>();
                        if (flipNameAndDescription.Contains(Object.SourceType))
                        {
                            dbDict.Add("name", Common.ReturnValueIfNull(gameObject.Description, ""));
                            dbDict.Add("description", Common.ReturnValueIfNull(gameObject.Name, ""));
                        }
                        else
                        {
                            dbDict.Add("name", Common.ReturnValueIfNull(gameObject.Name, ""));
                            dbDict.Add("description", Common.ReturnValueIfNull(gameObject.Description, ""));
                        }
                        dbDict.Add("year", Common.ReturnValueIfNull(gameObject.Year, ""));
                        dbDict.Add("publisher", Common.ReturnValueIfNull(gameObject.Publisher, ""));
                        dbDict.Add("demo", (int)gameObject.Demo);
                        dbDict.Add("system", Common.ReturnValueIfNull(gameObject.System, ""));
                        dbDict.Add("platform", Common.ReturnValueIfNull(gameObject.System, ""));
                        dbDict.Add("systemvariant", Common.ReturnValueIfNull(gameObject.SystemVariant, ""));
                        dbDict.Add("video", Common.ReturnValueIfNull(gameObject.Video, ""));
                        dbDict.Add("category", Common.ReturnValueIfNull(gameObject.Category, ""));
                        dbDict.Add("updatedat", now);

                        List<int> gameCountries = new List<int>();
                        if (
                            gameObject.Country != null &&
                            gameObject.Country.Count > 0
                            )
                        {
                            foreach (KeyValuePair<string, string> country in gameObject.Country)
                            {
                                int countryId = -1;
                                countryId = Common.GetLookupByCode(Common.LookupTypes.Country, (string)Common.ReturnValueIfNull(country.Key.Trim(), ""));
                                if (countryId == -1)
                                {
                                    countryId = Common.GetLookupByValue(Common.LookupTypes.Country, (string)Common.ReturnValueIfNull(country.Key.Trim(), ""));

                                    if (countryId == -1)
                                    {
                                        Logging.Log(Logging.LogType.Warning, "Signature Ingest", "Unable to locate country id for " + country.Key.Trim());
                                        sql = "INSERT INTO Country (`Code`, `Value`) VALUES (@code, @name); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                                        Dictionary<string, object> countryDict = new Dictionary<string, object>{
                                            { "code", country.Key.Trim() },
                                            { "name", country.Value.Trim() }
                                        };
                                        countryId = int.Parse((await db.ExecuteCMDAsync(sql, countryDict)).Rows[0][0].ToString());
                                    }
                                }

                                if (countryId > 0)
                                {
                                    gameCountries.Add(countryId);
                                }
                            }
                        }

                        List<int> gameLanguages = new List<int>();
                        if (
                            gameObject.Language != null &&
                            gameObject.Language.Count > 0
                            )
                        {
                            foreach (KeyValuePair<string, string> language in gameObject.Language)
                            {
                                int languageId = -1;
                                languageId = Common.GetLookupByCode(Common.LookupTypes.Language, (string)Common.ReturnValueIfNull(language.Key.Trim(), ""));
                                if (languageId == -1)
                                {
                                    languageId = Common.GetLookupByValue(Common.LookupTypes.Language, (string)Common.ReturnValueIfNull(language.Key.Trim(), ""));

                                    if (languageId == -1)
                                    {
                                        Logging.Log(Logging.LogType.Warning, "Signature Ingest", "Unable to locate language id for " + language.Key.Trim());
                                        sql = "INSERT INTO Language (`Code`, `Value`) VALUES (@code, @name); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                                        Dictionary<string, object> langDict = new Dictionary<string, object>{
                                                        { "code", language.Key.Trim() },
                                                        { "name", language.Value.Trim() }
                                                    };
                                        languageId = int.Parse((await db.ExecuteCMDAsync(sql, langDict)).Rows[0][0].ToString());
                                    }
                                }

                                if (languageId > 0)
                                {
                                    gameLanguages.Add(languageId);
                                }
                            }
                        }

                        dbDict.Add("copyright", Common.ReturnValueIfNull(gameObject.Copyright, ""));

                        // store platform
                        int gameSystem = 0;
                        if (gameObject.System != null)
                        {
                            sql = "SELECT `Id` FROM Signatures_Platforms WHERE `Platform`=@platform";

                            sigDB = await db.ExecuteCMDAsync(sql, dbDict);
                            if (sigDB.Rows.Count == 0)
                            {
                                // entry not present, insert it
                                sql = "INSERT INTO Signatures_Platforms (`Platform`) VALUES (@platform); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                                sigDB = await db.ExecuteCMDAsync(sql, dbDict);

                                gameSystem = Convert.ToInt32(sigDB.Rows[0][0]);
                            }
                            else
                            {
                                gameSystem = (int)sigDB.Rows[0][0];
                            }
                        }
                        dbDict.Add("systemid", gameSystem);

                        // store publisher
                        int gamePublisher = 0;
                        if (gameObject.Publisher != null)
                        {
                            sql = "SELECT * FROM Signatures_Publishers WHERE `Publisher`=@publisher";

                            sigDB = await db.ExecuteCMDAsync(sql, dbDict);
                            if (sigDB.Rows.Count == 0)
                            {
                                // entry not present, insert it
                                sql = "INSERT INTO Signatures_Publishers (`Publisher`) VALUES (@publisher); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                                sigDB = await db.ExecuteCMDAsync(sql, dbDict);
                                gamePublisher = Convert.ToInt32(sigDB.Rows[0][0]);
                            }
                            else
                            {
                                gamePublisher = (int)sigDB.Rows[0][0];
                            }
                        }
                        dbDict.Add("publisherid", gamePublisher);

                        // store game
                        long gameId = 0;
                        sql = "SELECT * FROM Signatures_Games WHERE `Name`=@name AND `Year`=@year AND `PublisherId`=@publisherid AND `SystemId`=@systemid";

                        sigDB = await db.ExecuteCMDAsync(sql, dbDict);

                        dbDict.Add("sigsource", XMLType);
                        dbDict.Add("sourceid", sourceId);

                        if (sigDB.Rows.Count == 0)
                        {
                            // entry not present, insert it
                            sql = "INSERT INTO Signatures_Games " +
                                "(`Name`, `Description`, `Year`, `PublisherId`, `Demo`, `SystemId`, `SystemVariant`, `Video`, `Copyright`, `Category`, `MetadataSource`, `SourceId`, `created_at`, `updated_at`) VALUES " +
                                "(@name, @description, @year, @publisherid, @demo, @systemid, @systemvariant, @video, @copyright, @category, @sigsource, @sourceid, @updatedat, @updatedat); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                            sigDB = await db.ExecuteCMDAsync(sql, dbDict);

                            gameId = Convert.ToInt32(sigDB.Rows[0][0]);
                        }
                        else
                        {
                            gameId = (long)sigDB.Rows[0]["Id"];
                            long gameSourceId = (long)sigDB.Rows[0]["SourceId"];
                            int gameMetadataSourceId = (int)sigDB.Rows[0]["MetadataSource"];

                            string gameSourceSql = "UPDATE Signatures_Games SET `Category`=@category, `MetadataSource`=@sigsource, `SourceId`=@sourceid, `updated_at`=@updatedat WHERE `Id`=@gameid;";
                            dbDict.Add("gameid", gameId);
                            await db.ExecuteCMDAsync(gameSourceSql, dbDict);
                        }

                        // insert countries
                        foreach (int gameCountry in gameCountries)
                        {
                            try
                            {
                                sql = "SELECT * FROM Signatures_Games_Countries WHERE GameId = @gameid AND CountryId = @Countryid";
                                Dictionary<string, object> countryDict = new Dictionary<string, object>{
                                                { "gameid", gameId },
                                                { "Countryid", gameCountry }
                                            };
                                if ((await db.ExecuteCMDAsync(sql, countryDict)).Rows.Count == 0)
                                {
                                    sql = "INSERT INTO Signatures_Games_Countries (GameId, CountryId) VALUES (@gameid, @Countryid)";
                                    await db.ExecuteCMDAsync(sql, countryDict);
                                }
                            }
                            catch
                            {
                                Console.WriteLine("Game id: " + gameId + " with Country " + gameCountry);
                            }
                        }

                        // insert languages
                        foreach (int gameLanguage in gameLanguages)
                        {
                            try
                            {
                                sql = "SELECT * FROM Signatures_Games_Languages WHERE GameId = @gameid AND LanguageId = @languageid";
                                Dictionary<string, object> langDict = new Dictionary<string, object>{
                                                { "gameid", gameId },
                                                { "languageid", gameLanguage }
                                            };
                                if ((await db.ExecuteCMDAsync(sql, langDict)).Rows.Count == 0)
                                {
                                    sql = "INSERT INTO Signatures_Games_Languages (GameId, LanguageId) VALUES (@gameid, @languageid)";
                                    await db.ExecuteCMDAsync(sql, langDict);
                                }
                            }
                            catch
                            {
                                Console.WriteLine("Game id: " + gameId + " with language " + gameLanguage);
                            }
                        }

                        // store rom
                        foreach (RomSignatureObject.Game.Rom romObject in gameObject.Roms)
                        {
                            if (romObject.Md5 != null || romObject.Sha1 != null || romObject.Sha256 != null || romObject.Crc != null)
                            {
                                long romId = 0;
                                sql = "SELECT * FROM Signatures_Roms WHERE `GameId`=@gameid AND ((`MD5`=@md5 AND `SHA1`=@sha1 AND `SHA256`=@sha256 AND `CRC`=@crc AND `IngestorVersion`=3) OR (`MD5`=@md5 AND `SHA1`=@sha1 AND `IngestorVersion`<=2));";
                                dbDict = new Dictionary<string, object>
                                {
                                    { "gameid", gameId },
                                    { "name", Common.ReturnValueIfNull(romObject.Name, "") },
                                    { "size", Common.ReturnValueIfNull(romObject.Size, 0) },
                                    { "crc", Common.ReturnValueIfNull(romObject.Crc, "").ToString().ToLower() },
                                    { "md5", Common.ReturnValueIfNull(romObject.Md5, "").ToString().ToLower() },
                                    { "sha1", Common.ReturnValueIfNull(romObject.Sha1, "").ToString().ToLower() },
                                    { "sha256", Common.ReturnValueIfNull(romObject.Sha256, "").ToString().ToLower() },
                                    { "developmentstatus", Common.ReturnValueIfNull(romObject.DevelopmentStatus, "") }
                                };

                                if (romObject.Attributes != null)
                                {
                                    if (romObject.Attributes.Count > 0)
                                    {
                                        dbDict.Add("attributes", Newtonsoft.Json.JsonConvert.SerializeObject(romObject.Attributes));
                                    }
                                    else
                                    {
                                        dbDict.Add("attributes", "");
                                    }
                                }
                                else
                                {
                                    dbDict.Add("attributes", "");
                                }
                                dbDict.Add("romtype", (int)romObject.RomType);
                                dbDict.Add("romtypemedia", Common.ReturnValueIfNull(romObject.RomTypeMedia, ""));
                                dbDict.Add("medialabel", Common.ReturnValueIfNull(romObject.MediaLabel, ""));
                                dbDict.Add("metadatasource", romObject.SignatureSource);
                                dbDict.Add("sourceid", sourceId);
                                dbDict.Add("status", Common.ReturnValueIfNull(romObject.Status, ""));
                                dbDict.Add("ingestorversion", 3);
                                dbDict.Add("updatedat", now);

                                Dictionary<string, string>? countries = new Dictionary<string, string>();
                                if (romObject.Country != null)
                                {
                                    countries = romObject.Country;
                                }
                                dbDict.Add("countries", Newtonsoft.Json.JsonConvert.SerializeObject(countries));

                                Dictionary<string, string>? languages = new Dictionary<string, string>();
                                if (romObject.Language != null)
                                {
                                    languages = romObject.Language;
                                }
                                dbDict.Add("languages", Newtonsoft.Json.JsonConvert.SerializeObject(languages));

                                sigDB = await db.ExecuteCMDAsync(sql, dbDict);
                                if (sigDB.Rows.Count == 0)
                                {
                                    // entry not present, insert it
                                    sql = "INSERT INTO Signatures_Roms (`GameId`, `Name`, `Size`, `CRC`, `MD5`, `SHA1`, `SHA256`, `Status`, `DevelopmentStatus`, `Attributes`, `RomType`, `RomTypeMedia`, `MediaLabel`, `MetadataSource`, `SourceId`, `IngestorVersion`, `Countries`, `Languages`, `created_at`, `updated_at`) VALUES (@gameid, @name, @size, @crc, @md5, @sha1, @sha256, @status, @developmentstatus, @attributes, @romtype, @romtypemedia, @medialabel, @metadatasource, @sourceid, @ingestorversion, @countries, @languages, @updatedat, @updatedat); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                                    sigDB = await db.ExecuteCMDAsync(sql, dbDict);

                                    romId = Convert.ToInt32(sigDB.Rows[0][0]);
                                    dbDict.Add("romid", romId);
                                }
                                else
                                {
                                    romId = (long)sigDB.Rows[0][0];
                                    dbDict.Add("romid", romId);

                                    // check if column IngesterVersion < 3, if so, we need to update the crc and sha256 values before proceeding
                                    if (sigDB.Rows[0]["IngestorVersion"] == DBNull.Value || Convert.ToInt32(sigDB.Rows[0]["IngestorVersion"]) < 3)
                                    {
                                        dbDict["crc"] = Common.ReturnValueIfNull(romObject.Crc, "").ToString().ToLower();
                                        dbDict["sha256"] = Common.ReturnValueIfNull(romObject.Sha256, "").ToString().ToLower();

                                        sql = "UPDATE Signatures_Roms SET `CRC`=@crc, `SHA256`=@sha256, `IngestorVersion`=3 WHERE `Id`=@romid;";
                                        await db.ExecuteCMDAsync(sql, dbDict);
                                    }

                                    // update the rom entry
                                    sql = "UPDATE Signatures_Roms SET `GameId`=@gameid, `Name`=@name, `Size`=@size, `Status`=@status, `DevelopmentStatus`=@developmentstatus, `Attributes`=@attributes, `RomType`=@romtype, `RomTypeMedia`=@romtypemedia, `MediaLabel`=@medialabel, `MetadataSource`=@metadatasource, `SourceId`=@sourceid, `Countries`=@countries, `Languages`=@languages, `updated_at`=@updatedat WHERE `Id`=@romid;";
                                    await db.ExecuteCMDAsync(sql, dbDict);
                                }

                                // map the rom to the source
                                sql = "SELECT * FROM Signatures_RomToSource WHERE SourceId=@sourceid AND RomId=@romid;";

                                sigDB = await db.ExecuteCMDAsync(sql, dbDict);
                                if (sigDB.Rows.Count == 0)
                                {
                                    sql = "INSERT INTO Signatures_RomToSource (`SourceId`, `RomId`) VALUES (@sourceid, @romid);";
                                    await db.ExecuteCMDAsync(sql, dbDict);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "Signature Ingest", "Error ingesting " + XMLType.ToString() + " file: " + XMLFile, ex);
            }
        }

        private async Task PruneOldSources(gaseous_signature_parser.parser.SignatureParser XMLType, Database db, DateTime now)
        {
            // get the most recent source entries for this XML type - anything with a processed_at value of now should be kept - everything else should be removed
            // this is the "safe" sources list - these should be kept - anything not in this list should be removed
            string sql = "SELECT * FROM Signatures_Sources WHERE `SourceType`=@sourcetype AND processed_at = @processedat;";
            DataTable dt = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>
            {
                { "sourcetype", XMLType.ToString() },
                { "processedat", now }
            });

            List<int> safeSourceIds = new List<int>();
            foreach (DataRow row in dt.Rows)
            {
                safeSourceIds.Add(Convert.ToInt32(row["Id"]));
            }

            if (safeSourceIds.Count == 0)
            {
                // nothing to keep, avoid deleting everything
                return;
            }

            // delete roms linked to sources not in the safe list
            bool deletionComplete = false;
            do
            {
                sql = "DELETE FROM Signatures_Roms WHERE MetadataSource=@sourcetype AND (SourceId NOT IN (" + string.Join(",", safeSourceIds) + ") OR SourceId IS NULL) LIMIT 1000; SELECT ROW_COUNT() AS Count;";
                DataTable deletedCountTable = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>
                {
                    { "sourcetype", XMLType }
                });
                if (deletedCountTable.Rows.Count == 0)
                {
                    deletionComplete = true;
                    continue;
                }
                long deletedCount = Convert.ToInt64(deletedCountTable.Rows[0]["Count"]);
                if (deletedCount == 0)
                {
                    deletionComplete = true;
                }
            } while (!deletionComplete);
        }
    }
}