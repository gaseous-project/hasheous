using Classes;

namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// Metadata provider implementation for the GiantBomb source.
    /// </summary>
    public class MetadataGiantBomb : IMetadata
    {
        /// <inheritdoc/>
        public Metadata.Communications.MetadataSources MetadataSource => Metadata.Communications.MetadataSources.GiantBomb;

        /// <inheritdoc/>
        public async Task<DataObjects.MatchItem> FindMatchItemAsync(hasheous_server.Models.DataObjectItem item, List<string> searchCandidates, Dictionary<string, object>? options = null)
        {
            hasheous_server.Classes.DataObjects.MatchItem? DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
            {
                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.NoMatch,
                MetadataId = ""
            };

            switch (item.ObjectType)
            {
                case DataObjects.DataObjectType.Platform:
                    foreach (string candidate in searchCandidates)
                    {
                        long Id = GiantBomb.MetadataQuery.PlatformLookup(candidate);
                        if (Id > 0)
                        {
                            DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                            {
                                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                MetadataId = Id.ToString()
                            };

                            break;
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
                        long gameId = GiantBomb.MetadataQuery.GameLookup(platformId, candidate);
                        if (gameId > 0)
                        {
                            DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                            {
                                MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                MetadataId = gameId.ToString()
                            };

                            break;
                        }
                    }
                    break;
            }

            return DataObjectSearchResults;
        }
    }
}