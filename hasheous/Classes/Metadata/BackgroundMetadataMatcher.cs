using System;
using System.IO;
using MySqlConnector;
using gaseous_signature_parser.models.RomSignatureObject;
using System.Data;
using Classes;
using IGDB;
using IGDB.Models;
using hasheous_server.Classes.Metadata.IGDB;
using hasheous_server.Models;

namespace BackgroundMetadataMatcher
{
    public class BackgroundMetadataMatcher
    {
        public BackgroundMetadataMatcher()
        {

        }

        /// <summary>
        /// The method used to match the signature to the IGDB source
        /// </summary>
        public enum MatchMethod
        {
            /// <summary>
            /// No match
            /// </summary>
            NoMatch = 0,

            /// <summary>
            /// Automatic matches are subject to change - depending on IGDB
            /// </summary>
            Automatic = 1,

            /// <summary>
            /// Manual matches will never change
            /// </summary>
            Manual = 2,

            /// <summary>
            /// Too many matches to successfully match
            /// </summary>
            AutomaticTooManyMatches = 3,

            /// <summary>
            /// Manually set by an admin - will never change unless set by an admin
            /// </summary>
            ManualByAdmin = 4,

            /// <summary>
            /// Match made by vote
            /// </summary>
            Voted = 5
        }

        public async void StartMatcher()
        {
            Random rand = new Random();

            // start matching signature platforms to IGDB metadata
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "SELECT * FROM Signatures_Platforms ORDER BY `Platform`";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();

            DataTable sigDb = db.ExecuteCMD(sql);
            
            foreach (DataRow row in sigDb.Rows)
            {
                // check if there is an existing match
                sql = "SELECT * FROM Match_SignaturePlatforms WHERE SignaturePlatformId=@signatureplatformid";
                dbDict.Clear();
                dbDict.Add("signatureplatformid", (int)row["Id"]);
                DataTable sigPlatformDb = db.ExecuteCMD(sql, dbDict);

                if (sigPlatformDb.Rows.Count == 0)
                {
                    // no match recorded - insert one (if we can)
                    Console.WriteLine("Searching for platform match for Signature platform: " + (string)row["Platform"]);
                    PerformPlatformSearch(
                        rand,
                        db,
                        row,
                        "INSERT INTO Match_SignaturePlatforms (SignaturePlatformId, IGDBPlatformId, MatchMethod, LastSearched, NextSearch) VALUES (@signatureplatformid, @igdbplatformid, @matchmethod, @lastsearched, @nextsearch);",
                        dbDict);
                }
                else
                {
                    // we have a match - do we need to update it?
                    switch ((MatchMethod)sigPlatformDb.Rows[0]["MatchMethod"])
                    {
                        case MatchMethod.Automatic:
                        case MatchMethod.AutomaticTooManyMatches:
                        case MatchMethod.Manual:
                            // no update required - changes should be up to the user
                            break;

                        case MatchMethod.NoMatch:
                            // no match - has it been more than 7 days but less than 14 days since the last search?
                            Console.WriteLine("Searching for platform match for Signature platform: " + (string)row["Platform"]);
                            if ((DateTime)sigPlatformDb.Rows[0]["NextSearch"] < DateTime.UtcNow)
                            {
                                PerformPlatformSearch(
                                    rand,
                                    db,
                                    row,
                                    "UPDATE Match_SignaturePlatforms SET IGDBPlatformId=@igdbplatformid, MatchMethod=@matchmethod, LastSearched=@lastsearched, NextSearch=@nextsearch WHERE SignaturePlatformId=@signatureplatformid;",
                                    dbDict
                                );
                            }
                            else
                            {
                                Console.WriteLine("Postponing update until " + (DateTime)sigPlatformDb.Rows[0]["NextSearch"]);
                            }
                            break;

                    }
                }
            }
        }

        private async void PerformPlatformSearch(Random rand, Database db, DataRow row, string sql, Dictionary<string, object> dbDict)
        {
            long platformMapId = -1;
            int hoursToAdd = rand.Next(168, 336);

            foreach (PlatformMapItem platformMapItem in JsonPlatformMap.PlatformMap)
            {
                if (
                    platformMapItem.IGDBName == ((string)row["Platform"]).ToLower() ||
                    platformMapItem.AlternateNames.Contains(((string)row["Platform"]).ToLower(), StringComparer.OrdinalIgnoreCase)
                )
                {
                    platformMapId = platformMapItem.IGDBId;
                    break;
                }
            }

            if (platformMapId == -1)
            {
                // no platform map match, perform search
                string searchPlatform = ((string)row["Platform"]).ToLower();
                Task<Platform[]> platforms = SearchForPlatform(searchPlatform);

                if (platforms != null)
                {
                    if (platforms.Result != null)
                    {
                        if (platforms.Result.Length == 0)
                        {
                            // no match
                            dbDict.Add("igdbplatformid", 0);
                            dbDict.Add("matchmethod", MatchMethod.NoMatch);
                            dbDict.Add("lastsearched", DateTime.UtcNow);
                            dbDict.Add("nextsearch", DateTime.UtcNow.AddHours(hoursToAdd));
                        }
                        else if (platforms.Result.Length == 1)
                        {
                            // exact match
                            dbDict.Add("igdbplatformid", platforms.Result[0].Id);
                            dbDict.Add("matchmethod", MatchMethod.Automatic);
                            dbDict.Add("lastsearched", DateTime.UtcNow);
                            dbDict.Add("nextsearch", DateTime.UtcNow.AddHours(hoursToAdd));

                            // get platform metadata
                            hasheous_server.Classes.Metadata.IGDB.Platforms.GetPlatform((long)platforms.Result[0].Id);
                        }
                        else
                        {
                            // multiple matches
                            
                            bool matchFound = false;
                            foreach (Platform platform in platforms.Result)
                            {
                                if (matchFound == false)
                                {
                                    if (platform.Name.ToLower() == ((string)row["Platform"]).ToLower())
                                    {
                                        // exact match
                                        dbDict.Add("igdbplatformid", platforms.Result[0].Id);
                                        dbDict.Add("matchmethod", MatchMethod.Automatic);
                                        dbDict.Add("lastsearched", DateTime.UtcNow);
                                        dbDict.Add("nextsearch", DateTime.UtcNow.AddHours(hoursToAdd));
                                        matchFound = true;
                                    }
                                }
                                hasheous_server.Classes.Metadata.IGDB.Platforms.GetPlatform((long)platform.Id);
                            }

                            if (matchFound == false)
                            {
                                dbDict.Add("igdbplatformid", 0);
                                dbDict.Add("matchmethod", MatchMethod.AutomaticTooManyMatches);
                                dbDict.Add("lastsearched", DateTime.UtcNow);
                                dbDict.Add("nextsearch", DateTime.UtcNow.AddHours(hoursToAdd));
                            }
                        }
                    }
                    else
                    {
                        // no match
                        dbDict.Add("igdbplatformid", 0);
                        dbDict.Add("matchmethod", MatchMethod.NoMatch);
                        dbDict.Add("lastsearched", DateTime.UtcNow);
                        dbDict.Add("nextsearch", DateTime.UtcNow.AddHours(hoursToAdd));
                    }
                }
            }
            else
            {
                // platform map defined
                dbDict.Add("igdbplatformid", platformMapId);
                dbDict.Add("matchmethod", MatchMethod.Automatic);
                dbDict.Add("lastsearched", DateTime.UtcNow);
                dbDict.Add("nextsearch", DateTime.UtcNow.AddHours(hoursToAdd));

                // get platform metadata
                hasheous_server.Classes.Metadata.IGDB.Platforms.GetPlatform(platformMapId);
            }
            db.ExecuteCMD(sql, dbDict);
        }

        private async Task<Platform[]> SearchForPlatform(string PlatformName)
        {
            Communications comms = new Communications(Communications.MetadataSources.IGDB);
            string searchBody = "search \"" + PlatformName + "\";";
            var results = await comms.APIComm<Platform>(IGDBClient.Endpoints.Platforms, "fields *;", searchBody);

            if (results == null || results.Length == 0)
            {
                searchBody = "where name ~ *\"" + PlatformName + "\"*;";
                results = await comms.APIComm<Platform>(IGDBClient.Endpoints.Platforms, "fields *;", searchBody);

                if (results == null || results.Length == 0)
                {
                    searchBody = "where name ~ \"" + PlatformName + "\";";
                    results = await comms.APIComm<Platform>(IGDBClient.Endpoints.Platforms, "fields *;", searchBody);
                }
            }

            return results;
        }
    }
}