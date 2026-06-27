namespace Classes
{
    public enum ResolvedStreamSource
    {
        Local,
        S3
    }

    /// <summary>
    /// Stream wrapper returned by the local/S3 fallback resolver.
    /// </summary>
    public sealed class ResolvedContentStream : IDisposable, IAsyncDisposable
    {
        private readonly Stream? _localStream;
        private readonly S3ObjectStream? _s3ObjectStream;

        private ResolvedContentStream(Stream localStream, long contentLength)
        {
            Source = ResolvedStreamSource.Local;
            _localStream = localStream;
            Stream = localStream;
            ContentLength = contentLength;
        }

        private ResolvedContentStream(S3ObjectStream s3ObjectStream)
        {
            Source = ResolvedStreamSource.S3;
            _s3ObjectStream = s3ObjectStream;
            Stream = s3ObjectStream.Stream;
            ContentLength = s3ObjectStream.ContentLength;
            ContentType = s3ObjectStream.ContentType;
            ETag = s3ObjectStream.ETag;
        }

        public ResolvedStreamSource Source { get; }
        public Stream Stream { get; }
        public long? ContentLength { get; }
        public string? ContentType { get; }
        public string? ETag { get; }

        public static ResolvedContentStream FromLocal(Stream stream, long contentLength)
        {
            return new ResolvedContentStream(stream, contentLength);
        }

        public static ResolvedContentStream FromS3(S3ObjectStream s3ObjectStream)
        {
            return new ResolvedContentStream(s3ObjectStream);
        }

        public void Dispose()
        {
            _localStream?.Dispose();
            _s3ObjectStream?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_localStream != null)
            {
                await _localStream.DisposeAsync();
            }

            if (_s3ObjectStream != null)
            {
                await _s3ObjectStream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Resolves file streams using local disk first, then S3 fallback.
    /// </summary>
    public class StorageFallbackResolver
    {
        private readonly S3StorageTools _s3StorageTools;

        public StorageFallbackResolver(S3StorageTools? s3StorageTools = null)
        {
            _s3StorageTools = s3StorageTools ?? new S3StorageTools();
        }

        /// <summary>
        /// Resolves a file stream by checking local disk first, then S3.
        /// The fallback resolver requires both bucketName and fileName for the S3 lookup.
        /// Returns null when neither local nor S3 has the requested file.
        /// </summary>
        public async Task<ResolvedContentStream?> ResolveReadStreamAsync(string localFilePath, string bucketName, string fileName, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(localFilePath) && File.Exists(localFilePath))
            {
                FileInfo fileInfo = new FileInfo(localFilePath);
                if (fileInfo.Length > 0)
                {
                    FileStream fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    return ResolvedContentStream.FromLocal(fileStream, fileInfo.Length);
                }

                // Remove invalid zero-length cache entries and continue fallback resolution.
                File.Delete(localFilePath);
            }

            if (!IsS3FallbackEnabled(bucketName, fileName))
            {
                return null;
            }

            S3ObjectStream? s3Stream = await _s3StorageTools.OpenReadStreamAsync(bucketName, fileName, cancellationToken);
            if (s3Stream != null)
            {
                // Read-through cache: local-miss + S3-hit stores locally for future requests.
                if (!string.IsNullOrWhiteSpace(localFilePath))
                {
                    bool storedLocally = await TryStoreS3StreamLocallyAsync(s3Stream, localFilePath, cancellationToken);
                    if (storedLocally && File.Exists(localFilePath))
                    {
                        FileInfo cachedFileInfo = new FileInfo(localFilePath);
                        if (cachedFileInfo.Length > 0)
                        {
                            FileStream localStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                            return ResolvedContentStream.FromLocal(localStream, cachedFileInfo.Length);
                        }
                    }

                    // If local cache write failed, the original S3 stream may have been consumed.
                    // Re-open from S3 so the caller still receives a readable stream.
                    await s3Stream.DisposeAsync();
                    s3Stream = await _s3StorageTools.OpenReadStreamAsync(bucketName, fileName, cancellationToken);
                    if (s3Stream == null)
                    {
                        return null;
                    }
                }

                return ResolvedContentStream.FromS3(s3Stream);
            }

            return null;
        }

        /// <summary>
        /// Uploads a local file to S3.
        /// Returns false when local file is missing, S3 is disabled, or overwrite is false and the object already exists.
        /// </summary>
        public async Task<bool> UploadLocalFileToS3Async(string localFilePath, string bucketName, string fileName, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            if (!Config.S3StorageConfiguration.Enabled)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
            {
                return false;
            }

            return await _s3StorageTools.UploadFileAsync(bucketName, fileName, localFilePath, overwrite, cancellationToken);
        }

        private static bool IsS3FallbackEnabled(string bucketName, string fileName)
        {
            if (!Config.S3StorageConfiguration.Enabled)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            return true;
        }

        private static async Task<bool> TryStoreS3StreamLocallyAsync(S3ObjectStream s3Stream, string localFilePath, CancellationToken cancellationToken)
        {
            try
            {
                string? localDirectory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrWhiteSpace(localDirectory) && !Directory.Exists(localDirectory))
                {
                    Directory.CreateDirectory(localDirectory);
                }

                string tempPath = localFilePath + ".tmp." + Guid.NewGuid().ToString("N");

                await using (FileStream localWriteStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await s3Stream.Stream.CopyToAsync(localWriteStream, 81920, cancellationToken);
                }

                if (File.Exists(localFilePath))
                {
                    File.Delete(localFilePath);
                }

                File.Move(tempPath, localFilePath);
                await s3Stream.DisposeAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
