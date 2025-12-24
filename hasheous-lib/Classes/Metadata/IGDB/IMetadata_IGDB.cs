

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
                    DataObjectSearchResults = await dataObjects.GetDataObject<IGDB.Models.Company>(Metadata.Communications.MetadataSources.IGDB, IGDB.IGDBClient.Endpoints.Companies, "fields *;", "where name ~ *\"" + item.Name + "\"");
                    break;
                case DataObjects.DataObjectType.Platform:
                    DataObjectSearchResults = await dataObjects.GetDataObject<IGDB.Models.Platform>(Metadata.Communications.MetadataSources.IGDB, IGDB.IGDBClient.Endpoints.Platforms, "fields *;", "where name ~ *\"" + item.Name + "\"");
                    break;
                case DataObjects.DataObjectType.Game:
                    bool searchComplete = false;

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
                    long platformId = (long)options["platformId"];

                    foreach (string candidate in searchCandidates)
                    {
                        foreach (Games.SearchType searchType in Enum.GetValues(typeof(Games.SearchType)))
                        {
                            IGDB.Models.Game[] games = Games.SearchForGame(candidate, platformId, searchType);

                            // check for matches
                            if (games != null)
                            {
                                if (games.Length == 1)
                                {
                                    // exact match found
                                    var idVal = games[0].Id;
                                    if (!idVal.HasValue)
                                    {
                                        // should not happen, but just in case
                                        continue;
                                    }
                                    long gameId = idVal.Value;

                                    DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                    {
                                        MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                        MetadataId = gameId.ToString()
                                    };
                                    searchComplete = true;
                                    break;
                                }
                                else if (games.Length > 1)
                                {
                                    // multiple matches found - high likelyhood of sequels and other variants - try and narrow it down a bit more
                                    foreach (var game in games)
                                    {
                                        var idVal = game.Id;
                                        if (!idVal.HasValue)
                                        {
                                            // should not happen, but just in case
                                            continue;
                                        }
                                        long gameId = idVal.Value;

                                        // check for exact name match
                                        if (string.Equals(game.Name, candidate, StringComparison.OrdinalIgnoreCase))
                                        {
                                            DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                            {
                                                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                                MetadataId = gameId.ToString()
                                            };
                                            searchComplete = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (searchComplete)
                            {
                                break;
                            }
                        }
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
    }
}