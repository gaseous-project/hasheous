using System.Data;
using static gaseous_signature_parser.models.provider.ScreenScaperModel;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches ScreenScraper metadata.
    /// </summary>
    public class FetchScreenScraperMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.SignatureIngestor
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            // get all of the ScreenScraper metadata in the cache
            string cachePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_Screenscraper, "games");
            if (!Directory.Exists(cachePath))
            {
                Logging.Log(Logging.LogType.Information, "Fetch ScreenScraper", "Cache directory not found, skipping: " + cachePath);
                return null;
            }

            string[] metadataFiles = Directory.GetFiles(cachePath, "*.json", SearchOption.AllDirectories);

            var parser = new gaseous_signature_parser.parser();
            var xmlIngestor = new XML.XMLIngestor();

            foreach (string metadataFile in metadataFiles)
            {
                try
                {
                    // convert the game metadata to a signature object
                    var signatureObject = parser.ParseSignatureDAT(metadataFile, Parser: gaseous_signature_parser.parser.SignatureParser.ScreenScraper);
                    if (signatureObject == null)
                    {
                        Logging.Log(Logging.LogType.Warning, "Fetch ScreenScraper", "Parsed metadata was null, skipping: " + metadataFile);
                        continue;
                    }

                    if (signatureObject.Games == null || signatureObject.Games.Count == 0)
                    {
                        Logging.Log(Logging.LogType.Warning, "Fetch ScreenScraper", "Parsed metadata contains no games, skipping: " + metadataFile);
                        continue;
                    }

                    if (signatureObject.Games[0].Roms == null || signatureObject.Games[0].Roms.Count == 0)
                    {
                        Logging.Log(Logging.LogType.Warning, "Fetch ScreenScraper", "Parsed metadata contains no roms, skipping: " + metadataFile);
                        continue;
                    }

                    DateTime now = DateTime.UtcNow;
                    bool processGames = false;
                    int sourceId = 0;

                    string sourceName = $"{signatureObject.SourceType} - {signatureObject.Games[0].System} - {signatureObject.Name}";

                    string sql = "SELECT * FROM Signatures_Sources WHERE `SourceMD5`=@sourcemd5";
                    Dictionary<string, object> dbDict = new Dictionary<string, object>
                    {
                        { "name", sourceName },
                        { "description", sourceName },
                        { "category", Common.ReturnValueIfNull(signatureObject.Category, "") },
                        { "version", Common.ReturnValueIfNull(signatureObject.Version, "") },
                        { "author", Common.ReturnValueIfNull(signatureObject.Author, "") },
                        { "email", Common.ReturnValueIfNull(signatureObject.Email, "") },
                        { "homepage", Common.ReturnValueIfNull(signatureObject.Homepage, "") }
                    };
                    if (signatureObject.Url == null)
                    {
                        dbDict.Add("uri", "");
                    }
                    else
                    {
                        dbDict.Add("uri", Common.ReturnValueIfNull(signatureObject.Url.ToString(), ""));
                    }
                    dbDict.Add("sourcetype", Common.ReturnValueIfNull(signatureObject.SourceType, ""));
                    dbDict.Add("processedat", now);
                    dbDict.Add("sourcemd5", signatureObject.SourceMd5);
                    dbDict.Add("sourcesha1", signatureObject.SourceSHA1);

                    DataTable sigDB = await Config.database.ExecuteCMDAsync(sql, dbDict);
                    if (sigDB.Rows.Count == 0)
                    {
                        // entry not present, insert it
                        sql = "INSERT INTO Signatures_Sources (`Name`, `Description`, `Category`, `Version`, `Author`, `Email`, `Homepage`, `Url`, `SourceType`, `processed_at`, `SourceMD5`, `SourceSHA1`) VALUES (@name, @description, @category, @version, @author, @email, @homepage, @uri, @sourcetype, @processedat, @sourcemd5, @sourcesha1); SELECT CAST(LAST_INSERT_ID() AS SIGNED);";

                        sigDB = await Config.database.ExecuteCMDAsync(sql, dbDict);

                        sourceId = Convert.ToInt32(sigDB.Rows[0][0]);

                        processGames = true;
                    }
                    else
                    {
                        // entry present, update processed date
                        sql = "UPDATE Signatures_Sources SET processed_at=@processedat WHERE `SourceMD5`=@sourcemd5;";
                        await Config.database.ExecuteCMDAsync(sql, dbDict);

                        sourceId = Convert.ToInt32(sigDB.Rows[0]["Id"]);
                    }

                    // store game records
                    if (signatureObject.Games != null && processGames)
                    {
                        foreach (var game in signatureObject.Games)
                        {
                            game.Description = ""; // clear description as it can be very long and we don't need it in the database

                            // reprocess the game metadata to store in the database
                            // language
                            Dictionary<string, string> languageDict = new Dictionary<string, string>();
                            if (game.Language != null)
                            {
                                foreach (var lang in game.Language.Keys)
                                {
                                    if (languageDict.ContainsKey(lang))
                                    {
                                        continue;
                                    }
                                    languageDict.Add(lang, Common.GetNameByCode(Common.LookupTypes.Language, lang));
                                }
                                game.Language = languageDict;
                            }
                            else
                            {
                                game.Language = new Dictionary<string, string>();
                            }
                            // country
                            Dictionary<string, string> countryDict = new Dictionary<string, string>();
                            if (game.Country != null)
                            {
                                foreach (var country in game.Country.Keys)
                                {
                                    if (countryDict.ContainsKey(country))
                                    {
                                        continue;
                                    }
                                    countryDict.Add(country, Common.GetNameByCode(Common.LookupTypes.Country, country));
                                }
                                game.Country = countryDict;
                            }
                            else
                            {
                                game.Country = new Dictionary<string, string>();
                            }

                            // process roms
                            foreach (var rom in game.Roms)
                            {
                                // language
                                Dictionary<string, string> romLanguageDict = new Dictionary<string, string>();
                                if (rom.Language != null)
                                {
                                    if (rom.Language.ContainsKey("regions_shortname"))
                                    {
                                        string lang = rom.Language["regions_shortname"];
                                        if (!romLanguageDict.ContainsKey(lang))
                                        {
                                            romLanguageDict.Add(lang, Common.GetNameByCode(Common.LookupTypes.Language, lang));
                                        }
                                    }
                                    rom.Language = romLanguageDict;
                                }
                                else
                                {
                                    rom.Language = new Dictionary<string, string>();
                                }
                                // country
                                Dictionary<string, string> romCountryDict = new Dictionary<string, string>();
                                if (rom.Country != null)
                                {
                                    if (rom.Country.ContainsKey("regions_shortname"))
                                    {
                                        string country = rom.Country["regions_shortname"];
                                        if (!romCountryDict.ContainsKey(country))
                                        {
                                            romCountryDict.Add(country, Common.GetNameByCode(Common.LookupTypes.Country, country));
                                        }
                                    }
                                    rom.Country = romCountryDict;
                                }
                                else
                                {
                                    rom.Country = new Dictionary<string, string>();
                                }
                            }

                            await xmlIngestor.ImportDatRecord(game, now, sourceId, gaseous_signature_parser.parser.SignatureParser.ScreenScraper);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log(Logging.LogType.Warning, "Fetch ScreenScraper", "Failed to deserialize ScreenScraper metadata file: " + metadataFile + " Exception: " + ex.Message);
                }
            }

            return null; // Assuming the method returns void, we return null here.
        }
    }
}