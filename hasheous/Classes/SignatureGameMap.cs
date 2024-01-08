using System;
using hasheous_server.Models;
using static BackgroundMetadataMatcher.BackgroundMetadataMatcher;

namespace Classes
{
    public static class SignatureGameMap
    {
        public static SignatureGameMapItem? GetSignatureGameMap(long SignatureGameId)
        {
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            Dictionary<string, object> dbDict = new Dictionary<string, object>();
            string sql = "SELECT * FROM Match_SignatureGames WHERE SignatureGameId = @signaturegameid;";
            dbDict.Add("signaturegameid", SignatureGameId);
            List<Dictionary<string, object>> sigMap = db.ExecuteCMDDict(sql, dbDict);
            
            if (sigMap.Count > 0)
            {
                SignatureGameMapItem gameMapItem = new SignatureGameMapItem();
                    gameMapItem.SignatureGameId = long.Parse(sigMap[0]["SignatureGameId"].ToString());
                    gameMapItem.IGDBGameId = long.Parse(sigMap[0]["IGDBGameId"].ToString());
                    gameMapItem.MatchMethod = (BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod)Enum.Parse(typeof(BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod), sigMap[0]["MatchMethod"].ToString());
                    gameMapItem.LastSearch = DateTime.Parse(sigMap[0]["LastSearched"].ToString());
                    gameMapItem.NextSearch = DateTime.Parse(sigMap[0]["NextSearch"].ToString());

                return gameMapItem;
            }
            else
            {
                return null;
            }
        }

        public static void SetSignatureGameMap(long SignatureGameId, long IGDBGameId, MatchMethod MatchMethod)
        {
            string sql = "";
            if (GetSignatureGameMap(SignatureGameId) == null)
            {
                // record doesn't exist - insert it
                sql = "INSERT INTO Match_SignatureGames (SignatureGameId, IGDBGameId, MatchMethod, LastSearched, NextSearch) VALUES (@signaturegameid, @igdbgameid, @matchmethod, @lastsearch, @nextsearch);";
            }
            else
            {
                // record exists - update it
                sql = "UPDATE Match_SignatureGames SET IGDBGameId = @igdbgameid, MatchMethod = @matchmethod, LastSearched = @lastsearch, NextSearch = @nextsearch WHERE SignatureGameId = @signaturegameid;";
            }
            Dictionary<string, object> dbDict = new Dictionary<string, object>
            {
                { "signaturegameid", SignatureGameId },
                { "igdbgameid", IGDBGameId },
                { "matchmethod", MatchMethod },
                { "lastsearch", DateTime.UtcNow },
                { "nextsearch", DateTime.UtcNow.AddDays(7) }
            };

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            db.ExecuteNonQuery(sql, dbDict);
        }
    }
}