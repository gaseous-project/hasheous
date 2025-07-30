
namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches metadata from IGDB.
    /// </summary>
    public class FetchIGDBMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public string TaskName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            InternetGameDatabase.DownloadManager igdbDownloader = new InternetGameDatabase.DownloadManager();
            await igdbDownloader.Download();

            return null;
        }
    }
}