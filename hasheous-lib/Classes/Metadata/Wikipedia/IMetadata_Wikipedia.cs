namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// Provides metadata lookup functionality using Wikipedia as a source.
    /// Implements IMetadata to locate and map external metadata for data objects.
    /// </summary>
    public class MetadataWikipedia : IMetadata
    {
        /// <inheritdoc/>
        public Metadata.Communications.MetadataSources MetadataSource => Metadata.Communications.MetadataSources.Wikipedia;

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
                case DataObjects.DataObjectType.Game:
                    // currently wikipedia metadata lookup is only available from IGDB game metadata
                    if (options == null || !options.ContainsKey("igdbGameId"))
                    {
                        throw new ArgumentException("IGDB Game ID must be provided in options for Wikipedia game search.");
                    }
                    // check that options["igdbGameId"] is a long
                    if (options["igdbGameId"] == null || options["igdbGameId"].GetType() != typeof(long))
                    {
                        throw new ArgumentException("IGDB Game ID must be of type long for Wikipedia game search.");
                    }
                    long igdbGameId = (long)options["igdbGameId"];
                    HasheousClient.Models.Metadata.IGDB.Game? igdbGame = await Metadata.IGDB.Metadata.GetMetadata<HasheousClient.Models.Metadata.IGDB.Game>(igdbGameId);
                    if (igdbGame != null)
                    {
                        // check the websites array for a wikipedia link
                        if (igdbGame.Websites != null && igdbGame.Websites.Count > 0)
                        {
                            foreach (var website in igdbGame.Websites)
                            {
                                HasheousClient.Models.Metadata.IGDB.Website? webGame = await Metadata.IGDB.Metadata.GetMetadata<HasheousClient.Models.Metadata.IGDB.Website>(website);
                                if (webGame != null)
                                {
                                    if (webGame.Type == 3)
                                    {
                                        DataObjectSearchResults = new hasheous_server.Classes.DataObjects.MatchItem
                                        {
                                            MatchMethod = BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod.Automatic,
                                            MetadataId = webGame.Url
                                        };
                                        break;
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