
namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches metadata from IGDB.
    /// </summary>
    public class FetchIGDBMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {

        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            InternetGameDatabase.DownloadManager igdbDownloader = new InternetGameDatabase.DownloadManager();
            await igdbDownloader.Download();

            return null;
        }
    }
}