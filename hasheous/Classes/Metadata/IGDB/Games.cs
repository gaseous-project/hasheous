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

            Game[] searchResults = [];

            // get Game metadata
            if (Config.IGDB.UseDumps == true && Config.IGDB.DumpsAvailable == true)
            {
                searchResults = await Metadata.GetObjectsFromDatabase<Game>(IGDBClient.Endpoints.Games, searchFields, searchBody);
            }
            else
            {
                Communications comms = new Communications(Communications.MetadataSources.IGDB);
                var results = await comms.APIComm<Game>(IGDBClient.Endpoints.Games, searchFields, searchBody);

                if (results != null && results.Length > 0)
                {
                    searchResults = results;
                }
            }

            if (searchResults == null || searchResults.Length == 0)
            {
                Logging.Log(Logging.LogType.Information, "Game Search", $"No games found for search string '{SearchString}' on platform {PlatformId} using search type {searchType}. Falling back to alternative names.");

                // try searching related tables
                var altNames = await _SearchForGameUsingRelatedTable<AlternativeName>(SearchString, searchType);

                if (altNames != null && altNames.Length > 0)
                {
                    // we have alternative names, so we can try to find games using these
                    List<Game> altNameGames = new List<Game>();
                    foreach (var altName in altNames)
                    {
                        var game = await Metadata.GetMetadata<Game>(altName.Game.Id);
                        if (game != null)
                        {
                            altNameGames.Add(game);
                        }
                    }

                    if (altNameGames.Count > 0)
                    {
                        searchResults = altNameGames.ToArray();
                    }
                }
                else
                {
                    Logging.Log(Logging.LogType.Information, "Game Search", $"No alternative names found for search string '{SearchString}' on platform {PlatformId}. Falling back to localised names.");

                    // try localised names
                    var localisedNames = await _SearchForGameUsingRelatedTable<GameLocalization>(SearchString, searchType);

                    if (localisedNames != null && localisedNames.Length > 0)
                    {
                        // we have localised names, so we can try to find games using these
                        List<Game> localisedGames = new List<Game>();
                        foreach (var localisedName in localisedNames)
                        {
                            var game = await Metadata.GetMetadata<Game>(localisedName.Game.Id);
                            if (game != null)
                            {
                                localisedGames.Add(game);
                            }
                        }

                        if (localisedGames.Count > 0)
                        {
                            searchResults = localisedGames.ToArray();
                        }
                    }
                }
            }

            return searchResults ?? Array.Empty<Game>();
        }

        private static async Task<T[]> _SearchForGameUsingRelatedTable<T>(string SearchString, SearchType searchType)
        {
            string searchBody = "";
            string searchFields = "fields *;";
            switch (searchType)
            {
                case SearchType.search:
                    searchBody = "search \"" + SearchString + "\"; ";
                    break;
                case SearchType.wherefuzzy:
                    searchBody = "where name ~ *\"" + SearchString + "\"*;";
                    break;
                case SearchType.where:
                    searchBody = "where name ~ \"" + SearchString + "\";";
                    break;
            }

            // get endpoint based on type
            string endpoint = Metadata.GetEndpointData<T>().Endpoint;

            // get Game metadata
            if (Config.IGDB.UseDumps == true && Config.IGDB.DumpsAvailable == true)
            {
                return await Metadata.GetObjectsFromDatabase<T>(endpoint, searchFields, searchBody);
            }
            else
            {
                Communications comms = new Communications(Communications.MetadataSources.IGDB);
                var results = await comms.APIComm<T>(endpoint, searchFields, searchBody);

                return results ?? Array.Empty<T>();
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