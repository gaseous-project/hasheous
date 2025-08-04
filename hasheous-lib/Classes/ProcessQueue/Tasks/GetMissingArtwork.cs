namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that retrieves missing artwork for games.
    /// </summary>
    public class GetMissingArtwork : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.MetadataMatchSearch,
            QueueItemType.TallyVotes
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            BackgroundMetadataMatcher.BackgroundMetadataMatcher tMatcher = new BackgroundMetadataMatcher.BackgroundMetadataMatcher();
            await tMatcher.GetGamesWithoutArtwork();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}