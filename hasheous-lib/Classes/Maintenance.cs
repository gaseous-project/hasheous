using System.Data;

namespace Classes
{
    /// <summary>
    /// Provides maintenance routines for cache cleanup, log purging, database optimization, and image migration.
    /// </summary>
    public class Maintenance
    {
        /// <summary>
        /// Performs hourly maintenance for the frontend, including cleaning bundle caches and removing old or oversized bundles.
        /// </summary>
        public async Task RunHourlyMaintenance_Frontend()
        {
            // clean the bundle cache
            // get the current bundle cache size
            if (Directory.Exists(Config.LibraryConfiguration.LibraryMetadataBundlesDirectory))
            {
                DirectoryInfo dirInfo = new DirectoryInfo(Config.LibraryConfiguration.LibraryMetadataBundlesDirectory);
                FileInfo[] bundleFiles = dirInfo.GetFiles("*.bundle");
                long totalSizeInBytes = bundleFiles.Sum(f => f.Length);
                long maxSizeInBytes = Config.MetadataConfiguration.MetadataBundle_MaxStorageInMB * 1024 * 1024;

                // delete old bundles if the total size exceeds the max size
                if (totalSizeInBytes > maxSizeInBytes)
                {
                    // order the files by last write time
                    var filesByAge = bundleFiles.OrderBy(f => f.LastWriteTime).ToList();
                    foreach (var file in filesByAge)
                    {
                        System.IO.File.Delete(file.FullName);
                        totalSizeInBytes -= file.Length;
                        if (totalSizeInBytes <= maxSizeInBytes)
                        {
                            break;
                        }
                    }
                }

                // delete bundles older than max age
                DateTime thresholdDate = DateTime.Now.AddDays(-Config.MetadataConfiguration.MetadataBundle_MaxAgeInDays);
                foreach (var file in bundleFiles)
                {
                    if (file.LastWriteTime < thresholdDate)
                    {
                        System.IO.File.Delete(file.FullName);
                    }
                }
            }

            // clean other caches if needed
            CleanupCachesBySize(new string[]
            {
                Config.LibraryConfiguration.LibraryTempDirectory,
                Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_GiantBomb, "Images"),
                Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB, "Images"),
                Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB, "Companies"),
                Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB, "Platforms"),
                Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB, "Games"),
                Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_TheGamesDb, "Images")
            }, Config.MetadataConfiguration.MetadataCache_MaxStorageInMB);
        }

        /// <summary>
        /// Performs hourly maintenance tasks such as aggregating insights into summary tables.
        /// </summary>
        public async Task RunHourlyMaintenance()
        {
            // aggregate insights into summary tables
            await Classes.Insights.Insights.AggregateHourlySummary();
        }

        /// <summary>
        /// Performs daily maintenance tasks such as purging logs, deleting old insights, and migrating images from the database to the filesystem.
        /// </summary>
        public async Task RunDailyMaintenance()
        {
            await Logging.PurgeLogsAsync();

            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

            // aggregate insights into summary tables
            await Classes.Insights.Insights.AggregateDailySummary();
            await Classes.Insights.Insights.AggregateMonthlySummary();

            // migrate images from database to filesystem
            string sql = "SELECT * FROM Images LIMIT 1000;";
            DataTable images = await db.ExecuteCMDAsync(sql);
            if (images.Rows.Count > 0)
            {
                Logging.Log(Logging.LogType.Information, "Maintenance", "Migrating images from database to filesystem");
                foreach (DataRow row in images.Rows)
                {
                    string imageId = row["Id"].ToString();
                    byte[] imageData = row["Content"] as byte[];
                    string extension = row["Extension"].ToString();
                    string filePath = Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_HasheousImages, imageId + extension);

                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            Logging.Log(Logging.LogType.Information, "Maintenance", "Deleted existing image: " + imageId + extension);
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                        await File.WriteAllBytesAsync(filePath, imageData);

                        // Update the database to remove the image content
                        sql = "DELETE FROM Images WHERE Id = @Id;";
                        Dictionary<string, object> parameters = new Dictionary<string, object>
                        {
                            { "@Id", imageId }
                        };
                        await db.ExecuteCMDAsync(sql, parameters);

                        Logging.Log(Logging.LogType.Information, "Maintenance", "Migrated image " + imageId + extension + " to filesystem.");
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "Maintenance", "Failed to migrate image " + imageId + extension + ": " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Performs weekly maintenance tasks, including optimizing all database tables.
        /// </summary>
        public async Task RunWeeklyMaintenance()
        {
            // optimise database tables
            Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);
            string sql = "";
            Dictionary<string, object> dbDict = new Dictionary<string, object>();

            Logging.Log(Logging.LogType.Information, "Maintenance", "Optimising database tables");
            sql = "SHOW FULL TABLES WHERE Table_Type = 'BASE TABLE';";
            DataTable tables = await db.ExecuteCMDAsync(sql);

            int StatusCounter = 1;
            foreach (DataRow row in tables.Rows)
            {
                Logging.SendReport(Config.LogName, StatusCounter, tables.Rows.Count, "Optimising table " + row[0].ToString(), true);
                sql = "OPTIMIZE TABLE " + row[0].ToString();
                DataTable response = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>(), 240);
                foreach (DataRow responseRow in response.Rows)
                {
                    string retVal = "";
                    for (int i = 0; i < responseRow.ItemArray.Length; i++)
                    {
                        retVal += responseRow.ItemArray[i] + "; ";
                    }
                    Logging.Log(Logging.LogType.Information, "Maintenance", "(" + StatusCounter + "/" + tables.Rows.Count + "): Optimise table " + row[0].ToString() + ": " + retVal);
                }

                StatusCounter += 1;
            }
        }

        /// <summary>
        /// Cleans up cache files in the specified directories, keeping total size under the given max MB.
        /// Files are sorted by last access time and oldest files are deleted first.
        /// </summary>
        /// <param name="cachePaths">Array of cache directory paths to clean up.</param>
        /// <param name="maxCacheSizeMB">Maximum total cache size in MB.</param>
        private void CleanupCachesBySize(string[]? cachePaths = null, int maxCacheSizeMB = 100)
        {
            // If no paths provided, use an empty array
            cachePaths ??= Array.Empty<string>();
            long maxSizeBytes = maxCacheSizeMB * 1024L * 1024L;
            var allFiles = new List<FileInfo>();
            foreach (var path in cachePaths)
            {
                if (!Directory.Exists(path)) continue;
                try
                {
                    var dirInfo = new DirectoryInfo(path);
                    allFiles.AddRange(dirInfo.GetFiles("*", SearchOption.AllDirectories));
                }
                catch { /* ignore errors for now */ }
            }
            // Sort files by last access time (oldest first)
            var filesByAccess = allFiles.OrderBy(f => f.LastAccessTimeUtc).ToList();
            long totalSize = filesByAccess.Sum(f => f.Length);
            int removedCount = 0;
            foreach (var file in filesByAccess)
            {
                if (totalSize <= maxSizeBytes) break;
                try
                {
                    file.Delete();
                    totalSize -= file.Length;
                    removedCount++;
                }
                catch { /* ignore errors for now */ }
            }
            // Recursively clean up empty child directories
            foreach (var path in cachePaths)
            {
                if (!Directory.Exists(path)) continue;
                try
                {
                    CleanupEmptyDirectoriesRecursively(path);
                }
                catch { /* ignore errors for now */ }
            }
            // Optionally log cleanup summary
            Logging.Log(Logging.LogType.Information, "Maintenance", $"Cache cleanup: {removedCount} files removed, final size: {totalSize / (1024 * 1024)} MB");
        }

        /// <summary>
        /// Recursively deletes empty directories under the given root path.
        /// </summary>
        /// <param name="rootPath">Root directory to clean.</param>
        private void CleanupEmptyDirectoriesRecursively(string rootPath)
        {
            // Remove empty child directories first
            foreach (var dir in Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                    {
                        Directory.Delete(dir, false);
                    }
                }
                catch { /* ignore errors for now */ }
            }
            // Then check and remove the root directory itself if empty
            try
            {
                if (Directory.Exists(rootPath) && Directory.GetFileSystemEntries(rootPath).Length == 0)
                {
                    Directory.Delete(rootPath, false);
                }
            }
            catch { /* ignore errors for now */ }
        }
    }
}