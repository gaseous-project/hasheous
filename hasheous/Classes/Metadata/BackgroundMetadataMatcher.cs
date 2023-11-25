using System;
using System.IO;
using MySqlConnector;
using gaseous_signature_parser.models.RomSignatureObject;
using System.Data;
using Classes;
using IGDB;
using IGDB.Models;
using gaseous_server.Classes.Metadata.IGDB;

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
            AutomaticTooManyMatches = 3
        }

        private static IGDBClient igdb = new IGDBClient(
                    // Found in Twitch Developer portal for your app
                    Config.IGDB.ClientId,
                    Config.IGDB.Secret
                );

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

        private async Task<Platform[]> SearchForPlatform(string PlatformName)
        {
            string searchBody = "fields *; search \"" + PlatformName + "\";";
            var results = await igdb.QueryAsync<Platform>(IGDBClient.Endpoints.Platforms, query: searchBody);

            if (results.Length == 0)
            {
                searchBody = "fields *; where name ~ *\"" + PlatformName + "\"*;";
                results = await igdb.QueryAsync<Platform>(IGDBClient.Endpoints.Platforms, query: searchBody);

                if (results.Length == 0)
                {
                    searchBody = "fields *; where name ~ \"" + PlatformName + "\";";
                    results = await igdb.QueryAsync<Platform>(IGDBClient.Endpoints.Platforms, query: searchBody);
                }
            }

            return results;
        }

        private void PerformPlatformSearch(Random rand, Database db, DataRow row, string sql, Dictionary<string, object> dbDict)
        {
            Task<IGDB.Models.Platform[]> platforms = SearchForPlatform(((string)row["Platform"]).ToLower());
            int hoursToAdd = rand.Next(168, 336);
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
                Platforms.GetPlatform((long)platforms.Result[0].Id);
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
                    Platforms.GetPlatform((long)platform.Id);
                }

                if (matchFound == false)
                {
                    dbDict.Add("igdbplatformid", 0);
                    dbDict.Add("matchmethod", MatchMethod.AutomaticTooManyMatches);
                    dbDict.Add("lastsearched", DateTime.UtcNow);
                    dbDict.Add("nextsearch", DateTime.UtcNow.AddHours(hoursToAdd));
                }
            }
            db.ExecuteCMD(sql, dbDict);
        }
    }
}