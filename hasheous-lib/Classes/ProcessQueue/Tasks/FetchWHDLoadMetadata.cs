namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches WHDLoad metadata.
    /// </summary>
    public class FetchWHDLoadMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.SignatureIngestor
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            WHDLoad.DownloadManager whdloadDownloader = new WHDLoad.DownloadManager();
            await whdloadDownloader.Download();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}