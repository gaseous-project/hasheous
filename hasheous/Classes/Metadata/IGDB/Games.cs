using System;
using System.Data;
using System.Threading.Tasks;
using Classes;
using Classes.Metadata;
using IGDB;
using IGDB.Models;

namespace hasheous_server.Classes.Metadata.IGDB
{
    public class Games
    {
        const string fieldList = "fields *;";

        public Games()
        {

        }

        public static Game[] SearchForGame(string SearchString, long PlatformId, SearchType searchType)
        {
            Task<Game[]> games = _SearchForGame(SearchString, PlatformId, searchType);
            return games.Result;
        }

        private static async Task<Game[]> _SearchForGame(string SearchString, long PlatformId, SearchType searchType)
        {
            string searchBody = "";
            string searchFields = "fields id,name,slug,platforms,summary; ";
            switch (searchType)
            {
                case SearchType.search:
                    searchBody = "search \"" + SearchString + "\"; ";
                    searchBody += "where platforms = (" + PlatformId + ");";
                    break;
                case SearchType.wherefuzzy:
                    searchBody = "where platforms = (" + PlatformId + ") & name ~ *\"" + SearchString + "\"*;";
                    break;
                case SearchType.where:
                    searchBody = "where platforms = (" + PlatformId + ") & name ~ \"" + SearchString + "\";";
                    break;
            }


            // get Game metadata
            if (Config.IGDB.UseDumps == true && Config.IGDB.DumpsAvailable == true)
            {
                return await Metadata.GetObjectsFromDatabase<Game>(IGDBClient.Endpoints.Games, searchFields, searchBody);
            }
            else
            {
                Communications comms = new Communications(Communications.MetadataSources.IGDB);
                var results = await comms.APIComm<Game>(IGDBClient.Endpoints.Games, searchFields, searchBody);

                return results;
            }
        }

        public enum SearchType
        {
            where = 0,
            wherefuzzy = 1,
            search = 2
        }
    }
}