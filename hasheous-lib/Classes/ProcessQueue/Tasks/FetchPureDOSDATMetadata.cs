namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches Redump metadata.
    /// </summary>
    public class FetchPureDOSDATMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {

        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            PureDOSDAT.DownloadManager mrDownloader = new PureDOSDAT.DownloadManager();
            await mrDownloader.Download();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}