namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// Provides metadata lookup functionality using TheGamesDB as a source.
    /// Implements IMetadata to locate and map external metadata for data objects.
    /// </summary>
    public class MetadataTheGamesDB : IMetadata
    {
        /// <inheritdoc/>
        public Metadata.Communications.MetadataSources MetadataSource => Metadata.Communications.MetadataSources.TheGamesDb;

        /// <inheritdoc/>
        public async Task<DataObjects.MatchItem> FindMatchItemAsync(hasheous_server.Models.DataObjectItem item, List<string> searchCandidates, Dictionary<string, object>? options = null)
        {
            TheGamesDB.SQL.MetadataQuery metadataQuery = new TheGamesDB.SQL.MetadataQuery();

            hasheous_server.Classes.DataObjects.MatchItem? DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };

            switch (item.ObjectType)
            {
                case DataObjects.DataObjectType.Platform:
                    var platformMatch = metadataQuery.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.Platforms>(new TheGamesDB.SQL.QueryModel
                    {
                        fieldList = ""
                    });

                    // no data returned
                    if (platformMatch == null || platformMatch.data == null || platformMatch.data.platforms == null)
                    {
                        return DataObjectSearchResults;
                    }

                    // search results
                    foreach (string candidate in searchCandidates)
                    {
                        foreach (var platform in platformMatch.data.platforms.Values)
                        {
                            if (string.Equals(platform.name, candidate, StringComparison.OrdinalIgnoreCase) || string.Equals(platform.alias, candidate, StringComparison.OrdinalIgnoreCase))
                            {
                                DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                {
                                    MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                    MetadataId = platform.id.ToString()
                                };
                            }
                        }
                    }

                    break;

                case DataObjects.DataObjectType.Game:
                    // needs to have a platformId option provided to search properly
                    if (options == null || !options.ContainsKey("platformId"))
                    {
                        throw new ArgumentException("Platform ID must be provided in options for TheGamesDB game search.");
                    }
                    // check that options["platformId"] is a long
                    if (options["platformId"] == null || options["platformId"].GetType() != typeof(long))
                    {
                        throw new ArgumentException("Platform ID must be of type long for TheGamesDB game search.");
                    }
                    long platformId = (long)options["platformId"];

                    foreach (string candidate in searchCandidates)
                    {
                        TheGamesDB.SQL.QueryModel queryModel = new TheGamesDB.SQL.QueryModel
                        {
                            query = platformId.ToString(),
                            queryField = TheGamesDB.SQL.QueryModel.QueryFieldName.platform_id,
                            fieldList = "",
                            includeList = "",
                            page = 1,
                            pageSize = 100
                        };

                        TheGamesDB.SQL.MetadataQuery query = new TheGamesDB.SQL.MetadataQuery();
                        HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID? games = query.GetMetadata<HasheousClient.Models.Metadata.TheGamesDb.GamesByGameID>(queryModel);

                        // no data returned
                        if (games == null || games.data == null || games.data.games == null)
                        {
                            continue;
                        }

                        // search results
                        if (games.data.games.Count == 1)
                        {
                            // exact match found
                            var game = games.data.games.First();
                            DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                            {
                                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                MetadataId = game.id.ToString()
                            };
                            break;
                        }
                        else if (games.data.games.Count > 1)
                        {
                            // multiple matches found - try and narrow it down a bit more
                            foreach (var game in games.data.games)
                            {
                                // check for exact name match
                                if (string.Equals(game.game_title, candidate, StringComparison.OrdinalIgnoreCase))
                                {
                                    DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                    {
                                        MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                        MetadataId = game.id.ToString()
                                    };
                                    break;
                                }
                                else
                                {
                                    // check alternate titles
                                    if (game.alternates != null)
                                    {
                                        foreach (var altTitle in game.alternates)
                                        {
                                            if (string.Equals(altTitle, candidate, StringComparison.OrdinalIgnoreCase))
                                            {
                                                DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                                {
                                                    MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                                    MetadataId = game.id.ToString()
                                                };
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    break;
            }

            return DataObjectSearchResults;
        }
    }
}