namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches WHDLoad metadata.
    /// </summary>
    public class FetchFBNEOMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {

        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            FBNEO.DownloadManager fbneoDownloader = new FBNEO.DownloadManager();
            await fbneoDownloader.Download();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}