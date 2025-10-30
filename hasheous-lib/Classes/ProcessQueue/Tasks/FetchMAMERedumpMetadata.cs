namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches Redump metadata.
    /// </summary>
    public class FetchMAMERedumpMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {

        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            MAMERedump.DownloadManager mrDownloader = new MAMERedump.DownloadManager();
            await mrDownloader.Download();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}