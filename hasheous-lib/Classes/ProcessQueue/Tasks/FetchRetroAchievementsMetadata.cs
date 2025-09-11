namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches RetroAchievements metadata.
    /// </summary>
    public class FetchRetroAchievementsMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {

        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            RetroAchievements.DownloadManager raDownloader = new RetroAchievements.DownloadManager();
            await raDownloader.Download();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}