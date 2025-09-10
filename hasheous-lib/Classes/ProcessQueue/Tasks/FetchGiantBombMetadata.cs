namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches metadata from GiantBomb and updates the database accordingly.
    /// </summary>
    public class FetchGiantBombMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>
        {

        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            GiantBomb.MetadataDownload gbDownloader = new GiantBomb.MetadataDownload();

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Company), "", "name");
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Image), "guid,original_url");
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.ImageTag));
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Platform), "", "name,guid");
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Game), "", "name,guid");
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Dlc), "", "name,guid");
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Rating), "", "name.guid");
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Release), "", "name,guid");
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Review), "", "guid");
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.UserReview), "", "guid");

            await gbDownloader.DownloadPlatforms();
            await gbDownloader.DownloadGames();
            await gbDownloader.DownloadSubTypes<GiantBomb.Models.GiantBombUserReviewResponse, GiantBomb.Models.UserReview>("user_reviews");

            // Update the last update time in tracking
            GiantBomb.MetadataDownload.SetTracking("GiantBomb_LastUpdate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

            return null; // Assuming the method returns void, we return null here.
        }
    }
}