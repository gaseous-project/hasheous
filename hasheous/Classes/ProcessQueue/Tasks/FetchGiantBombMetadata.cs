namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that fetches metadata from GiantBomb and updates the database accordingly.
    /// </summary>
        public class FetchGiantBombMetadata : IQueueTask
    {
        /// <inheritdoc/>
        public string TaskName { get; set; } = "FetchGiantBombMetadata";

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            GiantBomb.MetadataDownload gbDownloader = new GiantBomb.MetadataDownload();

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Company));
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Image), "guid,original_url");
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.ImageTag));
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Platform));
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Game));
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Dlc));
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Rating));
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Release));
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.Review));
            db.BuildTableFromType(gbDownloader.dbName, "", typeof(GiantBomb.Models.UserReview));

            await gbDownloader.DownloadPlatforms();
            await gbDownloader.DownloadGames();
            // await gbDownloader.DownloadSubTypes<GiantBomb.Models.GiantBombReviewResponse, GiantBomb.Models.Review>("reviews");
            await gbDownloader.DownloadSubTypes<GiantBomb.Models.GiantBombUserReviewResponse, GiantBomb.Models.UserReview>("user_reviews");
            await gbDownloader.DownloadImages();

            return null; // Assuming the method returns void, we return null here.
        }
    }
}