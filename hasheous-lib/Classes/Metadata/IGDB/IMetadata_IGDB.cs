

using Classes;
using hasheous_server.Classes.Metadata.IGDB;

namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// IGDB metadata provider that implements <see cref="IMetadata"/> to locate and return
    /// metadata matches from the IGDB source for data objects (companies, platforms, games).
    /// </summary>
    public class MetadataIGDB : IMetadata
    {
        /// <inheritdoc/>
        public Metadata.Communications.MetadataSources MetadataSource => Metadata.Communications.MetadataSources.IGDB;

        /// <inheritdoc/>
        public bool Enabled
        {
            get
            {
                return !String.IsNullOrEmpty(Config.IGDB.ClientId) && !String.IsNullOrEmpty(Config.IGDB.Secret);
            }
        }

        /// <inheritdoc/>
        public async Task<DataObjects.MatchItem> FindMatchItemAsync(hasheous_server.Models.DataObjectItem item, List<string> searchCandidates, Dictionary<string, object>? options = null)
        {
            DataObjects dataObjects = new DataObjects();

            hasheous_server.Classes.DataObjects.MatchItem? DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };

            switch (item.ObjectType)
            {
                case DataObjects.DataObjectType.Company:
                    foreach (string candidate in searchCandidates)
                    {
                        DataObjectSearchResults = await dataObjects.GetDataObject<IGDB.Models.Company>(Metadata.Communications.MetadataSources.IGDB, IGDB.IGDBClient.Endpoints.Companies, "fields *;", "where name ~ *\"" + candidate + "\"");
                        if (DataObjectSearchResults != null && DataObjectSearchResults.MatchMethod != BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch)
                        {
                            break;
                        }
                    }
                    if (DataObjectSearchResults == null || DataObjectSearchResults.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch)
                    {
                        DataObjectSearchResults = await dataObjects.GetDataObject<IGDB.Models.Company>(Metadata.Communications.MetadataSources.IGDB, IGDB.IGDBClient.Endpoints.Companies, "fields *;", "where name ~ *\"" + item.Name + "\"");
                    }
                    break;
                case DataObjects.DataObjectType.Platform:
                    foreach (string candidate in searchCandidates)
                    {
                        DataObjectSearchResults = await dataObjects.GetDataObject<IGDB.Models.Platform>(Metadata.Communications.MetadataSources.IGDB, IGDB.IGDBClient.Endpoints.Platforms, "fields *;", "where name ~ *\"" + candidate + "\"");
                        if (DataObjectSearchResults != null && DataObjectSearchResults.MatchMethod != BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch)
                        {
                            break;
                        }
                    }
                    if (DataObjectSearchResults == null || DataObjectSearchResults.MatchMethod == BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch)
                    {
                        DataObjectSearchResults = await dataObjects.GetDataObject<IGDB.Models.Platform>(Metadata.Communications.MetadataSources.IGDB, IGDB.IGDBClient.Endpoints.Platforms, "fields *;", "where name ~ *\"" + item.Name + "\"");
                    }
                    break;
                case DataObjects.DataObjectType.Game:
                    // needs to have a platformId option provided to search properly
                    if (options == null || !options.ContainsKey("platformId"))
                    {
                        throw new ArgumentException("Platform ID must be provided in options for IGDB game search.");
                    }
                    // check that options["platformId"] is a long
                    if (options["platformId"] == null || options["platformId"].GetType() != typeof(long))
                    {
                        throw new ArgumentException("Platform ID must be of type long for IGDB game search.");
                    }

                    DataObjects.MatchItem? gameMatch = FindGameMatch(searchCandidates, (long)options["platformId"]);
                    if (gameMatch != null)
                    {
                        DataObjectSearchResults = gameMatch;
                    }

                    break;
                default:
                    DataObjectSearchResults = new()
                    {
                        MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                        MetadataId = ""
                    };
                    break;
            }

            return DataObjectSearchResults;
        }

        /// <summary>
        /// Searches IGDB for a game, working through the candidates from most to least specific and,
        /// for each candidate, from the most to least precise search type. A result is only accepted
        /// when its name is a confident match for the candidate, so a lone result from a fuzzy or
        /// relevance-ranked search is no longer treated as an exact match.
        /// </summary>
        private static DataObjects.MatchItem? FindGameMatch(List<string> searchCandidates, long platformId)
        {
            foreach (string candidate in searchCandidates)
            {
                foreach (Games.SearchType searchType in Enum.GetValues(typeof(Games.SearchType)))
                {
                    IGDB.Models.Game[] games = Games.SearchForGame(candidate, platformId, searchType);

                    IGDB.Models.Game? match = SelectBestGameMatch(candidate, games);
                    if (match?.Id != null)
                    {
                        return new DataObjects.MatchItem
                        {
                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                            MetadataId = match.Id.Value.ToString()
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Picks the strongest confident name match from a set of IGDB search results, preferring the
        /// highest score and, on a tie, the shortest title.
        /// </summary>
        private static IGDB.Models.Game? SelectBestGameMatch(string candidate, IGDB.Models.Game[]? games)
        {
            IGDB.Models.Game? bestGame = null;
            int bestScore = int.MinValue;
            int bestNameLength = int.MaxValue;

            if (games == null)
            {
                return null;
            }

            foreach (IGDB.Models.Game game in games)
            {
                if (game == null || game.Id == null || string.IsNullOrWhiteSpace(game.Name))
                {
                    continue;
                }

                int score = Common.GetNumberAwareNameMatchScore(candidate, game.Name);
                if (score == int.MinValue)
                {
                    continue;
                }

                int nameLength = game.Name.Length;
                if (score > bestScore || (score == bestScore && nameLength < bestNameLength))
                {
                    bestScore = score;
                    bestNameLength = nameLength;
                    bestGame = game;
                }
            }

            return bestGame;
        }
    }
}