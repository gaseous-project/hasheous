namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// Provides metadata lookup capabilities and identifies the metadata source.
    /// Implementations can locate matching DataObjects.MatchItem instances for a given DataObjectType.
    /// </summary>
    public interface IMetadata
    {
        /// <summary>
        /// The source identifier for this metadata provider.
        /// </summary>
        public Metadata.Communications.MetadataSources MetadataSource { get; }

        /// <summary>
        /// Finds a matching DataObjects.MatchItem for the specified data object type.
        /// </summary>
        /// <param name="item">The data object item to find a match for.</param>
        /// <param name="searchCandidates">A list of candidate strings to search against.</param>
        /// <param name="options">Optional parameters to customize the search behavior.</param>
        /// <returns>A Task that resolves to the matching DataObjects.MatchItem, or null if none found.</returns>
        public Task<DataObjects.MatchItem> FindMatchItemAsync(hasheous_server.Models.DataObjectItem item, List<string> searchCandidates, Dictionary<string, object>? options = null);
    }
}