using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Classes
{
    /// <summary>
    /// Low-level tools for checking, uploading, and reading objects from S3-compatible storage.
    /// </summary>
    public class S3StorageTools
    {
        private readonly IAmazonS3 _client;

        public S3StorageTools(IAmazonS3? client = null)
        {
            _client = client ?? BuildClientFromConfig();
        }

        public static IAmazonS3 BuildClientFromConfig()
        {
            var s3Config = Config.S3StorageConfiguration;

            AmazonS3Config clientConfig = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(s3Config.Region),
                ForcePathStyle = s3Config.ForcePathStyle
            };

            if (!string.IsNullOrWhiteSpace(s3Config.ServiceUrl))
            {
                clientConfig.ServiceURL = s3Config.ServiceUrl;
            }

            AWSCredentials credentials;
            if (string.IsNullOrWhiteSpace(s3Config.AccessKey) || string.IsNullOrWhiteSpace(s3Config.SecretKey))
            {
                credentials = FallbackCredentialsFactory.GetCredentials();
            }
            else if (string.IsNullOrWhiteSpace(s3Config.SessionToken))
            {
                credentials = new BasicAWSCredentials(s3Config.AccessKey, s3Config.SecretKey);
            }
            else
            {
                credentials = new SessionAWSCredentials(s3Config.AccessKey, s3Config.SecretKey, s3Config.SessionToken);
            }

            return new AmazonS3Client(credentials, clientConfig);
        }

        /// <summary>
        /// Fast existence check using object metadata lookup.
        /// </summary>
        public async Task<bool> ContentExistsAsync(string bucketName, string fileName, CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
                    Key = fileName
                }, cancellationToken);

                return true;
            }
            catch (AmazonS3Exception ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.ErrorCode == "NotFound" ||
                ex.ErrorCode == "NoSuchKey" ||
                ex.ErrorCode == "NoSuchBucket")
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Uploads a local file to the target bucket and key.
        /// Returns false when overwrite is disabled and the key already exists.
        /// </summary>
        public async Task<bool> UploadFileAsync(string bucketName, string fileName, string localFilePath, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
            {
                throw new FileNotFoundException("Local file not found for S3 upload.", localFilePath);
            }

            if (!overwrite && await ContentExistsAsync(bucketName, fileName, cancellationToken))
            {
                return false;
            }

            try
            {
                PutObjectResponse response = await _client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileName,
                    FilePath = localFilePath
                }, cancellationToken);

                return response.HttpStatusCode == HttpStatusCode.OK || response.HttpStatusCode == HttpStatusCode.NoContent;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lists all objects below an S3 key prefix.
        /// </summary>
        public async Task<List<S3Object>> ListObjectsAsync(string bucketName, string prefix, CancellationToken cancellationToken = default)
        {
            List<S3Object> objects = new List<S3Object>();
            string? continuationToken = null;

            do
            {
                ListObjectsV2Response response = await _client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken
                }, cancellationToken);

                objects.AddRange(response.S3Objects);
                continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
            }
            while (continuationToken != null);

            return objects;
        }

        /// <summary>
        /// Deletes S3 objects in batches and returns the keys successfully deleted.
        /// </summary>
        public async Task<List<string>> DeleteObjectsAsync(string bucketName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            List<string> deletedKeys = new List<string>();

            foreach (string[] batch in keys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .Chunk(1000))
            {
                DeleteObjectsResponse response = await _client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = bucketName,
                    Objects = batch.Select(key => new KeyVersion { Key = key }).ToList()
                }, cancellationToken);

                deletedKeys.AddRange(response.DeletedObjects.Select(deleted => deleted.Key));
            }

            return deletedKeys;
        }

        /// <summary>
        /// Opens an object from S3 as a readable stream.
        /// Returns null if the object or bucket does not exist.
        /// </summary>
        public async Task<S3ObjectStream?> OpenReadStreamAsync(string bucketName, string fileName, CancellationToken cancellationToken = default)
        {
            try
            {
                GetObjectResponse response = await _client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileName
                }, cancellationToken);

                return new S3ObjectStream(response);
            }
            catch (AmazonS3Exception ex) when (
                ex.StatusCode == HttpStatusCode.NotFound ||
                ex.ErrorCode == "NotFound" ||
                ex.ErrorCode == "NoSuchKey" ||
                ex.ErrorCode == "NoSuchBucket")
            {
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Wraps a streamed S3 object response and owns disposal of the underlying response stream.
    /// </summary>
    public sealed class S3ObjectStream : IDisposable, IAsyncDisposable
    {
        private readonly GetObjectResponse _response;

        public S3ObjectStream(GetObjectResponse response)
        {
            _response = response;
        }

        public Stream Stream => _response.ResponseStream;
        public string? ContentType => _response.Headers.ContentType;
        public long ContentLength => _response.ContentLength;
        public string? ETag => _response.ETag;

        public void Dispose()
        {
            _response.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            _response.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
