using System.Data;
using System.Text.RegularExpressions;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;
using IGDB.Models;

namespace Classes
{
	public class HashLookup
    {
        public HashLookup()
        {

        }

        public HashLookup(HashLookupModel model)
        {
            // get the raw signature
            List<Signatures_Games> rawSignatures = GetRawSignatures(model);

            // narrow down the options
            Signatures_Games discoveredSignature = new Signatures_Games();
            if (rawSignatures.Count == 0)
            {
                Signature = null;
                MetadataResults = null;
            }
            else
            {
                if (rawSignatures.Count == 1)
                {
                    // only 1 signature found!
                    discoveredSignature = rawSignatures.ElementAt(0);
                }
                else if (rawSignatures.Count > 1)
                {
                    // more than one signature found - find one with highest score
                    foreach (Signatures_Games Sig in rawSignatures)
                    {
                        if (Sig.Score > discoveredSignature.Score)
                        {
                            discoveredSignature = Sig;
                        }
                    }
                }

                // should only have one signature now
                // get metadata
                List<SignatureLookupItem.MetadataResult> metadataResults = BuildMetaData(discoveredSignature);

                // build return item
                Signature = new SignatureLookupItem.SignatureResult(discoveredSignature);
                MetadataResults = metadataResults;
            }
        }

        public SignatureLookupItem.SignatureResult? Signature { get; set; }
        public List<SignatureLookupItem.MetadataResult>? MetadataResults { get; set; }

        private static List<Signatures_Games> GetRawSignatures(HashLookupModel model)
		{
            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            List<string> whereClauses = new List<string>();
            if (model.MD5 != null)
            {
                whereClauses.Add("Signatures_Roms.MD5 = @md5");
                dbDict.Add("md5", model.MD5);
            }
            else if (model.SHA1 != null)
            {
                whereClauses.Add("Signatures_Roms.SHA1 = @sha1");
                dbDict.Add("sha1", model.SHA1);
            }

            if (whereClauses.Count > 0)
            {
                // lookup the provided hashes
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
                string sql = "SELECT view_Signatures_Games.*, Signatures_Roms.Id AS romid, Signatures_Roms.Name AS romname, Signatures_Roms.Size, Signatures_Roms.CRC, Signatures_Roms.MD5, Signatures_Roms.SHA1, Signatures_Roms.DevelopmentStatus, Signatures_Roms.Attributes, Signatures_Roms.RomType, Signatures_Roms.RomTypeMedia, Signatures_Roms.MediaLabel, Signatures_Roms.MetadataSource FROM Signatures_Roms INNER JOIN view_Signatures_Games ON Signatures_Roms.GameId = view_Signatures_Games.Id WHERE " + string.Join(" OR ", whereClauses);

                DataTable sigDb = db.ExecuteCMD(sql, dbDict);

                List<Signatures_Games> GamesList = new List<Signatures_Games>();

                foreach (DataRow sigDbRow in sigDb.Rows)
                {
                    Signatures_Games.GameItem game = new Signatures_Games.GameItem
                    {
                        Id = (long)sigDbRow["Id"],
                        Name = (string)sigDbRow["Name"],
                        Description = (string)sigDbRow["Description"],
                        Year = (string)sigDbRow["Year"],
                        Publisher = (string)sigDbRow["Publisher"],
                        Demo = (Signatures_Games.GameItem.DemoTypes)(int)sigDbRow["Demo"],
                        SystemId = (int)sigDbRow["PlatformId"],
                        System = (string)sigDbRow["Platform"],
                        SystemVariant = (string)sigDbRow["SystemVariant"],
                        Video = (string)sigDbRow["Video"],
                        Country = (string)sigDbRow["Country"],
                        Language = (string)sigDbRow["Language"],
                        Copyright = (string)sigDbRow["Copyright"]
                    };
                    Signatures_Games.RomItem rom = new Signatures_Games.RomItem{
                        Id = (long)sigDbRow["romid"],
                        Name = (string)sigDbRow["romname"],
                        Size = (long)sigDbRow["Size"],
                        Crc = (string)sigDbRow["CRC"],
                        Md5 = ((string)sigDbRow["MD5"]).ToLower(),
                        Sha1 = ((string)sigDbRow["SHA1"]).ToLower(),
                        DevelopmentStatus = (string)sigDbRow["DevelopmentStatus"],
                        Attributes = Newtonsoft.Json.JsonConvert.DeserializeObject<List<KeyValuePair<string, object>>>((string)Common.ReturnValueIfNull(sigDbRow["Attributes"], "[]")),
                        RomType = (Signatures_Games.RomItem.RomTypes)(int)sigDbRow["RomType"],
                        RomTypeMedia = (string)sigDbRow["RomTypeMedia"],
                        MediaLabel = (string)sigDbRow["MediaLabel"],
                        SignatureSource = (Signatures_Games.RomItem.SignatureSourceType)(Int32)sigDbRow["MetadataSource"]
                    };

                    Signatures_Games gameItem = new Signatures_Games
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

        private List<SignatureLookupItem.MetadataResult> BuildMetaData(Signatures_Games GameSignature)
        {
            List<SignatureLookupItem.MetadataResult> results = new List<SignatureLookupItem.MetadataResult>();

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            
            Platform metadataPlatform = SearchForPlatform(db, GameSignature.Game);

            if (metadataPlatform != null)
            {
                // we have a platform - we can now look for the game

                results.AddRange(SearchForGame(db, (long)metadataPlatform.Id, BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic, GameSignature.Game));
            }

            return results;
        }

        private Platform? SearchForPlatform(Database db, Signatures_Games.GameItem gameItem)
        {
            // platform lookup
            if (gameItem.SystemId != null)
            {
                Platform? ReturnPlatform = null;

                // we have a signature based platform id
                // check if we already have a metadata objected mapped to this platform id
                Dictionary<string, object> dbDict = new Dictionary<string, object>();
                string sql = "SELECT * FROM Match_SignaturePlatforms JOIN Signatures_Platforms ON Match_SignaturePlatforms.SignaturePlatformId = Signatures_Platforms.Id WHERE Match_SignaturePlatforms.SignaturePlatformId = @signatureplatformid;";
                dbDict.Add("signatureplatformid", gameItem.SystemId);
                DataTable platformMapTable = db.ExecuteCMD(sql, dbDict);

                if (platformMapTable.Rows.Count > 0)
                {
                    if (platformMapTable.Rows[0]["IGDBPlatformId"] != DBNull.Value)
                    {
                        long mappedPlatformId = (long)platformMapTable.Rows[0]["IGDBPlatformId"];
                        if (mappedPlatformId != 0)
                        {
                            // we have a mapped platform
                            ReturnPlatform = Platforms.GetPlatform(mappedPlatformId, false);
                            
                            // return the platform if we get a value back
                            if (ReturnPlatform != null)
                            {
                                return ReturnPlatform;
                            }
                        }
                    }
                }
            }

            // if we got here, then no suitable match found in the database, and we have no platform signature
            return null;
        }

        private List<SignatureLookupItem.MetadataResult>? SearchForGame(Database db, long PlatformId, BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod PlatformMatchMethod, Signatures_Games.GameItem gameItem)
        {
            List<SignatureLookupItem.MetadataResult> results = new List<SignatureLookupItem.MetadataResult>();

            // check the signature to game map first
            SignatureGameMapItem mapItem = SignatureGameMap.GetSignatureGameMap((long)gameItem.Id);
            
            if (mapItem != null)
            {
                // signature map found - produce metadata results for each metadata provider

                // process IGDB
                if (mapItem.IGDBGameId != 0)
                {
                    // IGDB metadata linked
                    SignatureLookupItem.MetadataResult IGDBresult = new SignatureLookupItem.MetadataResult{
                        PlatformId = PlatformId,
                        PlatformMatchMethod = PlatformMatchMethod,
                        GameId = mapItem.IGDBGameId,
                        GameMatchMethod = mapItem.MatchMethod,
                        Source = Communications.MetadataSources.IGDB
                    };

                    results.Add(IGDBresult);
                }
                else
                {
                    // no IGDB metadata linked - search for game and link
                    results.AddRange(SearchAndLinkGame(db, PlatformId, PlatformMatchMethod, gameItem));
                }

                // FOR FUTURE - add structure similar to above from "process IGDB" down for other metadata sources

            }
            else
            {
                // no signature map found - search metadata sources
                // process IGDB
                results.AddRange(SearchAndLinkGame(db, PlatformId, PlatformMatchMethod, gameItem));
            }

            // return matches
            return results;
        }

        private IGDB.Models.Game IGDB_SearchForGame(string GameName, long PlatformId)
        {
            // search discovered game - case insensitive exact match first
            IGDB.Models.Game determinedGame = new IGDB.Models.Game();

            List<string> SearchCandidates = GetSearchCandidates(GameName);
            
            foreach (string SearchCandidate in SearchCandidates)
            {
                bool GameFound = false;

                Logging.Log(Logging.LogType.Information, "Import Game", "Searching for title: " + SearchCandidate);

                foreach (Games.SearchType searchType in Enum.GetValues(typeof(Games.SearchType)))
                {
                    Logging.Log(Logging.LogType.Information, "Import Game", "Search type: " + searchType.ToString());
                    IGDB.Models.Game[] games = Games.SearchForGame(SearchCandidate, PlatformId, searchType);
                    if (games.Length == 1)
                    {
                        // exact match!
                        determinedGame = Games.GetGame((long)games[0].Id, false, false, false);
                        Logging.Log(Logging.LogType.Information, "Import Game", "IGDB game: " + determinedGame.Name);
                        GameFound = true;
                        break;
                    }
                    else if (games.Length > 0)
                    {
                        Logging.Log(Logging.LogType.Information, "Import Game", "  " + games.Length + " search results found");

                        // quite likely we've found sequels and alternate types
                        foreach (Game game in games) {
                            if (game.Name == SearchCandidate) {
                                // found game title matches the search candidate
                                determinedGame = Games.GetGame((long)games[0].Id, false, false, false);
                                Logging.Log(Logging.LogType.Information, "Import Game", "Found exact match!");
                                GameFound = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        Logging.Log(Logging.LogType.Information, "Import Game", "No search results found");
                    }
                }
                if (GameFound == true) { break; }
            }
            if (determinedGame == null)
            {
                determinedGame = new IGDB.Models.Game();
            }

            string destSlug = "";
            if (determinedGame.Id == null)
            {
                Logging.Log(Logging.LogType.Information, "Import Game", "Unable to determine game");
            }

            return determinedGame;
        }

        private static List<string> GetSearchCandidates(string GameName)
        {
            // remove version numbers from name
            GameName = Regex.Replace(GameName, @"v(\d+\.)?(\d+\.)?(\*|\d+)$", "").Trim();
            GameName = Regex.Replace(GameName, @"Rev (\d+\.)?(\d+\.)?(\*|\d+)$", "").Trim();

            // assumption: no games have () in their titles so we'll remove them
            int idx = GameName.IndexOf('(');
            if (idx >= 0) {
                GameName = GameName.Substring(0, idx);
            }

            List<string> SearchCandidates = new List<string>();
            SearchCandidates.Add(GameName.Trim());
            if (GameName.Contains(" - "))
            {
                SearchCandidates.Add(GameName.Replace(" - ", ": ").Trim());
                SearchCandidates.Add(GameName.Substring(0, GameName.IndexOf(" - ")).Trim());
            }
            if (GameName.Contains(": "))
            {
                SearchCandidates.Add(GameName.Substring(0, GameName.IndexOf(": ")).Trim());
            }

            Logging.Log(Logging.LogType.Information, "Import Game", "Search candidates: " + String.Join(", ", SearchCandidates));

            return SearchCandidates;
        }

        private List<SignatureLookupItem.MetadataResult>? SearchAndLinkGame(Database db, long PlatformId, BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod PlatformMatchMethod, Signatures_Games.GameItem gameItem)
        {
            List<SignatureLookupItem.MetadataResult> results = new List<SignatureLookupItem.MetadataResult>();

            Game? IGDBgame = IGDB_SearchForGame(gameItem.Name, PlatformId);
            if (IGDBgame != null)
            {
                if (IGDBgame.Id == null)
                {
                    // no match found :(
                    // set IGDB match to 0
                    SignatureGameMap.SetSignatureGameMap((long)gameItem.Id, 0, BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch);
                }
                else
                {
                    // match found! :)
                    SignatureGameMap.SetSignatureGameMap((long)gameItem.Id, (long)IGDBgame.Id, BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic);

                    // return metadataresult
                    SignatureLookupItem.MetadataResult IGDBresult = new SignatureLookupItem.MetadataResult{
                        PlatformId = PlatformId,
                        PlatformMatchMethod = PlatformMatchMethod,
                        GameId = (long)IGDBgame.Id,
                        GameMatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                        Source = Communications.MetadataSources.IGDB
                    };

                    results.Add(IGDBresult);
                }
            }

            return results;
        }
    }
}