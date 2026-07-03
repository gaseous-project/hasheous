using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using hasheous_server.Classes;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Classes
{
    /// <summary>
    /// Defines cache policy types for different content types and storage tiers.
    /// </summary>
    public enum CachePolicyType
    {
        /// <summary>
        /// Media files (images, audio, video) that never change.
        /// Tier 1: Size-only LRU. Tier 2: Size + 2-year age LRU.
        /// </summary>
        Media,

        /// <summary>
        /// Bundle files (generated ZIP + JSON metadata).
        /// Both tiers: Size + 90-day age LRU.
        /// </summary>
        Bundles
    }

    /// <summary>
    /// Configuration for a single cache tier (local disk or S3).
    /// </summary>
    public class CacheTierConfiguration
    {
        /// <summary>
        /// Maximum cache size in bytes before LRU eviction triggers.
        /// </summary>
        [JsonProperty("MaxSizeBytes")]
        public long MaxSizeBytes { get; set; }

        /// <summary>
        /// Maximum age of cached files in days (null = no age limit).
        /// Files older than this are candidates for eviction.
        /// </summary>
        [JsonProperty("MaxAgeDays")]
        public int? MaxAgeDays { get; set; }

        /// <summary>
        /// For Tier 1 (local disk): minimum free disk space to maintain in bytes.
        /// Eviction continues even if under MaxSizeBytes if free space drops below this.
        /// Default: 1 GB. Ignored for Tier 2 (S3).
        /// </summary>
        [JsonProperty("MinFreeDiskSpaceBytes")]
        public long MinFreeDiskSpaceBytes { get; set; } = 1L * 1024 * 1024 * 1024; // 1 GB

        /// <summary>
        /// Validates configuration values.
        /// </summary>
        public bool IsValid()
        {
            return MaxSizeBytes > 0 && (!MaxAgeDays.HasValue || MaxAgeDays > 0);
        }
    }

    /// <summary>
    /// Policies for different cache types and tiers.
    /// </summary>
    public class CachePolicies
    {
        /// <summary>Media cache policy tier configurations.</summary>
        [JsonProperty("Media")]
        public MediaPolicyConfig Media { get; set; } = new MediaPolicyConfig();

        /// <summary>Bundle cache policy tier configurations.</summary>
        [JsonProperty("Bundles")]
        public BundlesPolicyConfig Bundles { get; set; } = new BundlesPolicyConfig();

        /// <summary>Media policy (immutable content: images, audio, video) tier configurations.</summary>
        public class MediaPolicyConfig
        {
            /// <summary>Tier 1 (local disk).</summary>
            [JsonProperty("Tier1Local")]
            public CacheTierConfiguration Tier1Local { get; set; } = new CacheTierConfiguration
            {
                MaxSizeBytes = 10L * 1024 * 1024 * 1024,     // 10 GB
                MaxAgeDays = null                              // No age limit
            };

            /// <summary>Tier 2 (S3).</summary>
            [JsonProperty("Tier2S3")]
            public CacheTierConfiguration Tier2S3 { get; set; } = new CacheTierConfiguration
            {
                MaxSizeBytes = 1L * 1024 * 1024 * 1024 * 1024, // 1 TB
                MaxAgeDays = 365 * 2                            // 2 years
            };
        }

        /// <summary>Bundle policy (generated content: metadata + images zipped) tier configurations.</summary>
        public class BundlesPolicyConfig
        {
            /// <summary>Tier 1 (local disk).</summary>
            [JsonProperty("Tier1Local")]
            public CacheTierConfiguration Tier1Local { get; set; } = new CacheTierConfiguration
            {
                MaxSizeBytes = 10L * 1024 * 1024 * 1024,       // 10 GB
                MaxAgeDays = 90                                 // 90 days
            };

            /// <summary>Tier 2 (S3).</summary>
            [JsonProperty("Tier2S3")]
            public CacheTierConfiguration Tier2S3 { get; set; } = new CacheTierConfiguration
            {
                MaxSizeBytes = 1L * 1024 * 1024 * 1024 * 1024, // 1 TB
                MaxAgeDays = 90                                 // 90 days
            };
        }
    }

    /// <summary>
    /// Centralized cache management for proxy metadata files with tiered (local/S3) policies.
    /// Provides download+cache, read with local/S3 fallback, and disk-aware LRU maintenance.
    /// </summary>
    public static class ProxyCacheManager
    {
        private static readonly object _maintenanceLock = new object();

        // ===== DOWNLOAD & CACHE =====

        /// <summary>
        /// Downloads a file from the given URL, stores it locally in the source-specific cache directory,
        /// and schedules an S3 upload on HTTP response completion (non-blocking for client).
        /// </summary>
        public static async Task<ResolvedContentStream?> DownloadAndCacheAsync(
            string downloadUrl,
            string sourceKey,
            string resourcePath,
            CachePolicyType policyType,
            string? contentType = null,
            HttpContext? httpContext = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string localFilePath = BuildLocalFilePath(sourceKey, resourcePath);
                string? localDirectory = Path.GetDirectoryName(localFilePath);

                if (!string.IsNullOrWhiteSpace(localDirectory) && !Directory.Exists(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                }

                // Download to temp file first
                string tempPath = localFilePath + ".tmp." + Guid.NewGuid().ToString("N");
                bool downloadSuccess = await DownloadTools.DownloadFile(new Uri(downloadUrl), tempPath) ?? false;

                if (!downloadSuccess || !File.Exists(tempPath))
                {
                    return null;
                }

                // Validate file size > 0
                FileInfo tempFileInfo = new FileInfo(tempPath);
                if (tempFileInfo.Length == 0)
                {
                    File.Delete(tempPath);
                    return null;
                }

                // Atomically move to final location
                if (File.Exists(localFilePath))
                {
                    File.Delete(localFilePath);
                }
                File.Move(tempPath, localFilePath, overwrite: true);

                // Read the cached file
                FileInfo fileInfo = new FileInfo(localFilePath);
                FileStream fileStream = new FileStream(
                    localFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                ResolvedContentStream result = ResolvedContentStream.FromLocal(fileStream, fileInfo.Length);

                // Schedule S3 upload via response completion (non-blocking)
                if (httpContext != null)
                {
                    ScheduleS3Upload(localFilePath, sourceKey, resourcePath, httpContext);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "ProxyCacheManager", $"DownloadAndCacheAsync failed for {downloadUrl}: {ex.Message}");
                return null;
            }
        }

        // ===== READ WITH FALLBACK =====

        /// <summary>
        /// Resolves a file from cache (local or S3 fallback) for reading.
        /// Returns null if file not found anywhere.
        /// </summary>
        public static async Task<ResolvedContentStream?> ResolveReadAsync(
            string sourceKey,
            string resourcePath,
            CachePolicyType policyType,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            StorageFallbackResolver resolver = new StorageFallbackResolver();
            string bucketName = Config.S3StorageConfiguration.DefaultBucket;
            string s3Key = BuildS3Key(sourceKey, resourcePath);
            string localFilePath = BuildLocalFilePath(sourceKey, resourcePath);

            ResolvedContentStream? resolved = await resolver.ResolveReadStreamAsync(localFilePath, bucketName, s3Key);
            return resolved;
        }

        // ===== MAINTENANCE =====

        /// <summary>
        /// Runs cache maintenance for all policies and tiers.
        /// Returns (FilesDeletedLocal, FilesDeletedS3, BytesFreedLocal, BytesFreedS3).
        /// </summary>
        public static async Task<(long FilesLocal, long FilesS3, long BytesLocal, long BytesS3)> RunMaintenanceAsync(
            CancellationToken cancellationToken = default)
        {
            if (!Monitor.TryEnter(_maintenanceLock, TimeSpan.Zero))
            {
                // Already running
                return (0, 0, 0, 0);
            }

            try
            {
                long totalFilesLocal = 0, totalFilesS3 = 0, totalBytesLocal = 0, totalBytesS3 = 0;

                // Run maintenance for Media policy
                var (filesLocal, filesS3, bytesLocal, bytesS3) = await RunPolicyMaintenanceAsync(CachePolicyType.Media, cancellationToken);
                totalFilesLocal += filesLocal;
                totalFilesS3 += filesS3;
                totalBytesLocal += bytesLocal;
                totalBytesS3 += bytesS3;

                // Run maintenance for Bundles policy
                (filesLocal, filesS3, bytesLocal, bytesS3) = await RunPolicyMaintenanceAsync(CachePolicyType.Bundles, cancellationToken);
                totalFilesLocal += filesLocal;
                totalFilesS3 += filesS3;
                totalBytesLocal += bytesLocal;
                totalBytesS3 += bytesS3;

                return (totalFilesLocal, totalFilesS3, totalBytesLocal, totalBytesS3);
            }
            finally
            {
                Monitor.Exit(_maintenanceLock);
            }
        }

        /// <summary>
        /// Runs maintenance for a specific policy (all tiers).
        /// </summary>
        private static async Task<(long FilesLocal, long FilesS3, long BytesLocal, long BytesS3)> RunPolicyMaintenanceAsync(
            CachePolicyType policyType,
            CancellationToken cancellationToken)
        {
            var (tier1Config, tier2Config) = GetPolicyConfig(policyType);
            string[] sourceKeys = GetSourceKeysForPolicy(policyType);

            long filesLocal = 0, filesS3 = 0, bytesLocal = 0, bytesS3 = 0;

            // Run Tier 1 (local disk) maintenance
            foreach (string sourceKey in sourceKeys)
            {
                var (fl, bl) = await RunLocalMaintenanceAsync(sourceKey, tier1Config);
                filesLocal += fl;
                bytesLocal += bl;
            }

            // Run Tier 2 (S3) maintenance (placeholder for now)
            // S3 lifecycle policies can be configured instead

            return (filesLocal, filesS3, bytesLocal, bytesS3);
        }

        /// <summary>
        /// Runs local disk maintenance: age-based filtering, then disk-space-aware LRU eviction.
        /// </summary>
        private static async Task<(long FilesDeleted, long BytesFreed)> RunLocalMaintenanceAsync(
            string sourceKey,
            CacheTierConfiguration tierConfig)
        {
            string cacheDir = GetCacheDirectoryForSource(sourceKey);
            if (!Directory.Exists(cacheDir))
            {
                return (0, 0);
            }

            long filesDeleted = 0;
            long bytesFreed = 0;

            try
            {
                var files = new DirectoryInfo(cacheDir).GetFiles("*", SearchOption.AllDirectories);

                // Step 1: Apply age filter if configured
                if (tierConfig.MaxAgeDays.HasValue)
                {
                    DateTime cutoffDate = DateTime.Now.AddDays(-tierConfig.MaxAgeDays.Value);
                    foreach (var file in files.Where(f => f.LastWriteTime < cutoffDate).ToList())
                    {
                        try
                        {
                            long fileSize = file.Length;
                            file.Delete();
                            filesDeleted++;
                            bytesFreed += fileSize;
                        }
                        catch (Exception ex)
                        {
                            Logging.Log(Logging.LogType.Warning, "ProxyCacheManager", $"Failed to delete aged file {file.FullName}: {ex.Message}");
                        }
                    }
                    files = new DirectoryInfo(cacheDir).GetFiles("*", SearchOption.AllDirectories);
                }

                // Step 2: Check cache size + free disk space, evict LRU if needed
                long cacheSize = files.Sum(f => f.Length);
                long freeDiskSpace = GetFreeDiskSpace(cacheDir);

                if (cacheSize > tierConfig.MaxSizeBytes || freeDiskSpace < tierConfig.MinFreeDiskSpaceBytes)
                {
                    // Sort by LastAccessTime (or LastWriteTime if unavailable)
                    var sortedFiles = files.OrderBy(f => f.LastAccessTime != DateTime.MinValue ? f.LastAccessTime : f.LastWriteTime).ToList();

                    foreach (var file in sortedFiles)
                    {
                        if (cacheSize <= tierConfig.MaxSizeBytes && freeDiskSpace >= tierConfig.MinFreeDiskSpaceBytes)
                        {
                            break; // Stop evicting
                        }

                        try
                        {
                            long fileSize = file.Length;
                            file.Delete();
                            filesDeleted++;
                            bytesFreed += fileSize;
                            cacheSize -= fileSize;
                            freeDiskSpace = GetFreeDiskSpace(cacheDir); // Recheck
                        }
                        catch (Exception ex)
                        {
                            Logging.Log(Logging.LogType.Warning, "ProxyCacheManager", $"Failed to evict LRU file {file.FullName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "ProxyCacheManager", $"RunLocalMaintenanceAsync failed for {sourceKey}: {ex.Message}");
            }

            return (filesDeleted, bytesFreed);
        }

        // ===== HELPERS =====

        /// <summary>Gets the cache directory for a source key.</summary>
        private static string GetCacheDirectoryForSource(string sourceKey)
        {
            return sourceKey switch
            {
                "IGDB" => Config.LibraryConfiguration.LibraryMetadataDirectory_IGDB,
                "TheGamesDB" => Config.LibraryConfiguration.LibraryMetadataDirectory_TheGamesDb,
                "GiantBomb" => Config.LibraryConfiguration.LibraryMetadataDirectory_GiantBomb,
                "Screenscraper" => Config.LibraryConfiguration.LibraryMetadataDirectory_Screenscraper,
                "Bundles" => Config.LibraryConfiguration.LibraryMetadataBundlesDirectory,
                _ => Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory, sourceKey)
            };
        }

        /// <summary>Builds the local file path for a cached resource.</summary>
        private static string BuildLocalFilePath(string sourceKey, string resourcePath)
        {
            string cacheDir = GetCacheDirectoryForSource(sourceKey);
            return Path.Combine(cacheDir, resourcePath);
        }

        /// <summary>Builds the S3 key (path) for a cached resource.</summary>
        private static string BuildS3Key(string sourceKey, string resourcePath)
        {
            return $"{sourceKey}/{resourcePath}";
        }

        /// <summary>Gets free disk space in bytes for a directory.</summary>
        private static long GetFreeDiskSpace(string path)
        {
            try
            {
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(path) ?? "/");
                return drive.AvailableFreeSpace;
            }
            catch
            {
                return long.MaxValue; // Assume unlimited if unable to check
            }
        }

        /// <summary>Gets tier configurations for a policy type.</summary>
        private static (CacheTierConfiguration Tier1, CacheTierConfiguration Tier2) GetPolicyConfig(CachePolicyType policyType)
        {
            return policyType switch
            {
                CachePolicyType.Media => (Config.CachePolicies.Media.Tier1Local, Config.CachePolicies.Media.Tier2S3),
                CachePolicyType.Bundles => (Config.CachePolicies.Bundles.Tier1Local, Config.CachePolicies.Bundles.Tier2S3),
                _ => throw new ArgumentException($"Unknown policy type: {policyType}")
            };
        }

        /// <summary>Gets source keys for a policy type.</summary>
        private static string[] GetSourceKeysForPolicy(CachePolicyType policyType)
        {
            return policyType switch
            {
                CachePolicyType.Media => new[] { "IGDB", "TheGamesDB", "GiantBomb", "Screenscraper" },
                CachePolicyType.Bundles => new[] { "Bundles" },
                _ => Array.Empty<string>()
            };
        }

        /// <summary>Schedules an S3 upload via HTTP response completion (non-blocking).</summary>
        private static void ScheduleS3Upload(string localFilePath, string sourceKey, string resourcePath, HttpContext httpContext)
        {
            if (!Config.S3StorageConfiguration.Enabled)
            {
                return;
            }

            try
            {
                httpContext.Response.OnCompleted(async () =>
                {
                    try
                    {
                        StorageFallbackResolver resolver = new StorageFallbackResolver();
                        string bucketName = Config.S3StorageConfiguration.DefaultBucket;
                        string s3Key = BuildS3Key(sourceKey, resourcePath);

                        await resolver.UploadLocalFileToS3Async(localFilePath, bucketName, s3Key, overwrite: false);
                    }
                    catch (Exception ex)
                    {
                        Logging.Log(Logging.LogType.Warning, "ProxyCacheManager", $"S3 upload failed for {sourceKey}/{resourcePath}: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "ProxyCacheManager", $"Failed to schedule S3 upload: {ex.Message}");
            }
        }
    }
}
