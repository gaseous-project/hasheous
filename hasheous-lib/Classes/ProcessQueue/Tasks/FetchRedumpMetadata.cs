namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches Redump metadata.
    /// </summary>
    public class FetchRedumpMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.SignatureIngestor
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            Redump.DownloadManager rdDownloader = new Redump.DownloadManager();
            await rdDownloader.Download();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}