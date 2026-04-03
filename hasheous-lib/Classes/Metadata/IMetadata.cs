namespace hasheous_server.Classes.MetadataLib
{
    /// <summary>
    /// Thrown by an <see cref="IMetadata"/> implementation when the upstream API rate limit has been
    /// reached or approached, and the caller should defer the search until <see cref="RetryAfter"/>.
    /// </summary>
    public class MetadataRateLimitException : Exception
    {
        /// <summary>
        /// The earliest UTC time at which the caller should retry the operation.
        /// </summary>
        public DateTime RetryAfter { get; }

        /// <summary>
        /// Initialises a new instance with an optional retry-after hint.
        /// </summary>
        /// <param name="message">Human-readable description of the limit that was hit.</param>
        /// <param name="retryAfter">Earliest UTC time to retry; defaults to 1 hour from now if not supplied.</param>
        public MetadataRateLimitException(string message, DateTime? retryAfter = null)
            : base(message)
        {
            RetryAfter = retryAfter ?? DateTime.UtcNow.AddHours(1);
        }
    }

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
        /// Indicates whether this metadata provider is currently enabled and should be used for lookups.
        /// </summary>
        public bool Enabled { get; }

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