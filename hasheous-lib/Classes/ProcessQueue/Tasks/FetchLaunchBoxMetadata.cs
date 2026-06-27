namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches metadata from LaunchBox.
    /// </summary>
    public class FetchLaunchBoxMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {

        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            LaunchBox.DownloadManager launchBoxDownloader = new LaunchBox.DownloadManager();
            await launchBoxDownloader.Download();

            return null;
        }
    }
}