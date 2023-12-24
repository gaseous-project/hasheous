using System;
using System.IO;
using MySqlConnector;
using gaseous_signature_parser.models.RomSignatureObject;
using System.Data;
using Classes;

namespace XML
{
    public class XMLIngestor
    {
        public void Import(string SearchPath, gaseous_signature_parser.parser.SignatureParser XMLType)
        {
            // connect to database
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            // process provided files
            if (!Directory.Exists(SearchPath))
            {
                Directory.CreateDirectory(SearchPath);
            }

            string[] PathContents = Directory.GetFiles(SearchPath);
            Array.Sort(PathContents);

            string sql = "";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            System.Data.DataTable sigDB;

            for (UInt16 i = 0; i < PathContents.Length; ++i)
            {
                string XMLFile = PathContents[i];

                Logging.Log(Logging.LogType.Information, "Signature Ingest", "(" + (i + 1) + " / " + PathContents.Length + ") Processing " + XMLType.ToString() + " DAT file: " + XMLFile);

                // check xml file md5
                Common.hashObject hashObject = new Common.hashObject(XMLFile);
                sql = "SELECT * FROM Signatures_Sources WHERE SourceMD5=@sourcemd5";
                dbDict = new Dictionary<string, object>();
                dbDict.Add("sourcemd5", hashObject.md5hash);
                sigDB = db.ExecuteCMD(sql, dbDict);

                if (sigDB.Rows.Count == 0)
                {
                    try
                    {
                        // start parsing file
                        gaseous_signature_parser.parser Parser = new gaseous_signature_parser.parser();
                        RomSignatureObject Object = Parser.ParseSignatureDAT(XMLFile, XMLType);

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
                            dbDict = new Dictionary<string, object>();
                            dbDict.Add("name", Common.ReturnValueIfNull(Object.Name, ""));
                            dbDict.Add("description", Common.ReturnValueIfNull(Object.Description, ""));
                            dbDict.Add("category", Common.ReturnValueIfNull(Object.Category, ""));
                            dbDict.Add("version", Common.ReturnValueIfNull(Object.Version, ""));
                            dbDict.Add("author", Common.ReturnValueIfNull(Object.Author, ""));
                            dbDict.Add("email", Common.ReturnValueIfNull(Object.Email, ""));
                            dbDict.Add("homepage", Common.ReturnValueIfNull(Object.Homepage, ""));
                            if (Object.Url == null)
                            {
                                dbDict.Add("uri", "");
                            }
                            else
                            {
                                dbDict.Add("uri", Common.ReturnValueIfNull(Object.Url.ToString(), ""));
                            }
                            dbDict.Add("sourcetype", Common.ReturnValueIfNull(Object.SourceType, ""));
                            dbDict.Add("sourcemd5", Object.SourceMd5);
                            dbDict.Add("sourcesha1", Object.SourceSHA1);

                            sigDB = db.ExecuteCMD(sql, dbDict);
                            if (sigDB.Rows.Count == 0)
                            {
                                // entry not present, insert it
                                sql = "INSERT INTO Signatures_Sources (`Name`, `Description`, `Category`, `Version`, `Author`, `Email`, `Homepage`, `Url`, `SourceType`, `SourceMD5`, `SourceSHA1`) VALUES (@name, @description, @category, @version, @author, @email, @homepage, @uri, @sourcetype, @sourcemd5, @sourcesha1); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";

                                sigDB = db.ExecuteCMD(sql, dbDict);

                                sourceId = Convert.ToInt32(sigDB.Rows[0][0]);

                                processGames = true;
                            }

                            if (processGames == true)
                            {
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
                                    dbDict.Add("country", Common.ReturnValueIfNull(gameObject.Country, ""));
                                    dbDict.Add("language", Common.ReturnValueIfNull(gameObject.Language, ""));
                                    dbDict.Add("copyright", Common.ReturnValueIfNull(gameObject.Copyright, ""));

                                    // store platform
                                    int gameSystem = 0;
                                    if (gameObject.System != null)
                                    {
                                        sql = "SELECT `Id` FROM Signatures_Platforms WHERE `Platform`=@platform";

                                        sigDB = db.ExecuteCMD(sql, dbDict);
                                        if (sigDB.Rows.Count == 0)
                                        {
                                            // entry not present, insert it
                                            sql = "INSERT INTO Signatures_Platforms (`Platform`) VALUES (@platform); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                                            sigDB = db.ExecuteCMD(sql, dbDict);

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

                                        sigDB = db.ExecuteCMD(sql, dbDict);
                                        if (sigDB.Rows.Count == 0)
                                        {
                                            // entry not present, insert it
                                            sql = "INSERT INTO Signatures_Publishers (`Publisher`) VALUES (@publisher); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                                            sigDB = db.ExecuteCMD(sql, dbDict);
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

                                    sigDB = db.ExecuteCMD(sql, dbDict);
                                    if (sigDB.Rows.Count == 0)
                                    {
                                        // entry not present, insert it
                                        sql = "INSERT INTO Signatures_Games " +
                                            "(`Name`, `Description`, `Year`, `PublisherId`, `Demo`, `SystemId`, `SystemVariant`, `Video`, `Country`, `Language`, `Copyright`) VALUES " +
                                            "(@name, @description, @year, @publisherid, @demo, @systemid, @systemvariant, @video, @country, @language, @copyright); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                                        sigDB = db.ExecuteCMD(sql, dbDict);

                                        gameId = Convert.ToInt32(sigDB.Rows[0][0]);
                                    }
                                    else
                                    {
                                        gameId = (long)sigDB.Rows[0][0];
                                    }

                                    // store rom
                                    foreach (RomSignatureObject.Game.Rom romObject in gameObject.Roms)
                                    {
                                        if (romObject.Md5 != null || romObject.Sha1 != null)
                                        {
                                            long romId = 0;
                                            sql = "SELECT * FROM Signatures_Roms WHERE `GameId`=@gameid AND (`MD5`=@md5 OR `SHA1`=@sha1)";
                                            dbDict = new Dictionary<string, object>();
                                            dbDict.Add("gameid", gameId);
                                            dbDict.Add("name", Common.ReturnValueIfNull(romObject.Name, ""));
                                            dbDict.Add("size", Common.ReturnValueIfNull(romObject.Size, ""));
                                            dbDict.Add("crc", Common.ReturnValueIfNull(romObject.Crc, "").ToString().ToLower());
                                            dbDict.Add("md5", Common.ReturnValueIfNull(romObject.Md5, "").ToString().ToLower());
                                            dbDict.Add("sha1", Common.ReturnValueIfNull(romObject.Sha1, "").ToString().ToLower());
                                            dbDict.Add("developmentstatus", Common.ReturnValueIfNull(romObject.DevelopmentStatus, ""));

                                            if (romObject.Attributes != null)
                                            {
                                                if (romObject.Attributes.Count > 0)
                                                {
                                                    dbDict.Add("attributes", Newtonsoft.Json.JsonConvert.SerializeObject(romObject.Attributes));
                                                }
                                                else
                                                {
                                                    dbDict.Add("attributes", "[ ]");
                                                }
                                            }
                                            else
                                            {
                                                dbDict.Add("attributes", "[ ]");
                                            }
                                            dbDict.Add("romtype", (int)romObject.RomType);
                                            dbDict.Add("romtypemedia", Common.ReturnValueIfNull(romObject.RomTypeMedia, ""));
                                            dbDict.Add("medialabel", Common.ReturnValueIfNull(romObject.MediaLabel, ""));
                                            dbDict.Add("metadatasource", romObject.SignatureSource);
                                            dbDict.Add("ingestorversion", 2);

                                            sigDB = db.ExecuteCMD(sql, dbDict);
                                            if (sigDB.Rows.Count == 0)
                                            {
                                                // entry not present, insert it
                                                sql = "INSERT INTO Signatures_Roms (`GameId`, `Name`, `Size`, `CRC`, `MD5`, `SHA1`, `DevelopmentStatus`, `Attributes`, `RomType`, `RomTypeMedia`, `MediaLabel`, `MetadataSource`, `IngestorVersion`) VALUES (@gameid, @name, @size, @crc, @md5, @sha1, @developmentstatus, @attributes, @romtype, @romtypemedia, @medialabel, @metadatasource, @ingestorversion); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";
                                                sigDB = db.ExecuteCMD(sql, dbDict);

                                                romId = Convert.ToInt32(sigDB.Rows[0][0]);
                                            }
                                            else
                                            {
                                                romId = (long)sigDB.Rows[0][0];
                                            }

                                            // map the rom to the source
                                            sql = "SELECT * FROM Signatures_RomToSource WHERE SourceId=@sourceid AND RomId=@romid;";
                                            dbDict.Add("romid", romId);
                                            dbDict.Add("sourceId", sourceId);

                                            sigDB = db.ExecuteCMD(sql, dbDict);
                                            if (sigDB.Rows.Count == 0)
                                            {
                                                sql = "INSERT INTO Signatures_RomToSource (`SourceId`, `RomId`) VALUES (@sourceid, @romid);";
                                                db.ExecuteCMD(sql, dbDict);
                                            }
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
                else
                {
                    Logging.Log(Logging.LogType.Information, "Signature Ingest", "Rejecting already imported " + XMLType.ToString() + " file: " + XMLFile);
                }
            }
        }
    }
}