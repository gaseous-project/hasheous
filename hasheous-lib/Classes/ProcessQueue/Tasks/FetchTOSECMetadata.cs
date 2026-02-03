namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches TOSEC metadata.
    /// </summary>
    public class FetchTOSECMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {
            QueueItemType.SignatureIngestor
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            TOSEC.DownloadManager tosecDownloader = new TOSEC.DownloadManager();
            await tosecDownloader.Download();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}