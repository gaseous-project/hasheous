namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches metadata from TheGamesDB.
    /// </summary>
    public class FetchTheGamesDbMetadata
    {
        /// <inheritdoc/>
        public string TaskName { get; set; } = "FetchTheGamesDbMetadata";

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            // set up JSON
            TheGamesDB.JSON.DownloadManager tgdbDownloader = new TheGamesDB.JSON.DownloadManager();
            await tgdbDownloader.Download();

            // set up SQL
            TheGamesDB.SQL.DownloadManager tgdbSQLDownloader = new TheGamesDB.SQL.DownloadManager();
            await tgdbSQLDownloader.Download();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}