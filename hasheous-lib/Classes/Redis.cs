using System.Buffers;
using System.IO.Compression;
using System.Text;
using Classes;
using StackExchange.Redis;

namespace hasheous.Classes
{
    /// <summary>
    /// Provides a centralized, lazily-initialized Redis connection and simple
    /// cache helpers for key generation, retrieval, storage, and purge operations.
    /// </summary>
    /// <remarks>
    /// - Connection settings are sourced from <see cref="Config.RedisConfiguration"/>.
    /// - All operations respect Config.RedisConfiguration.Enabled and will no-op when disabled.
    /// - Keys are typically composed via <see cref="GenerateKey(string, object)"/> using a logical prefix.
    /// </remarks>
    public class RedisConnection
    {
        private static Lazy<ConnectionMultiplexer> lazyConnection;
        private const int PurgeBatchSize = 500;

        static RedisConnection()
        {
            lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
            {
                // Replace with your Redis connection string if needed
                string redisConnectionString = Config.RedisConfiguration.HostName + ":" + Config.RedisConfiguration.Port;
                return ConnectionMultiplexer.Connect(redisConnectionString);
            });
        }

        /// <summary>
        /// Gets the singleton <see cref="ConnectionMultiplexer"/> instance, created on first access.
        /// </summary>
        /// <remarks>
        /// Connection string is built from Config.RedisConfiguration.HostName and Config.RedisConfiguration.Port.
        /// </remarks>
        private static ConnectionMultiplexer Connection => lazyConnection.Value;

        /// <summary>
        /// Retrieves an <see cref="IDatabase"/> reference for the given logical database index.
        /// </summary>
        /// <param name="db">The Redis logical database number. Use <c>-1</c> to select the default database.</param>
        /// <returns>An <see cref="IDatabase"/> for executing Redis commands.</returns>
        private static IDatabase GetDatabase(int db = -1)
        {
            return Connection.GetDatabase(db);
        }

        /// <summary>
        /// Generates a cache key by serializing the <paramref name="key"/> object to JSON,
        /// base64-encoding it, and optionally prefixing with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">An optional logical prefix to namespace the key (e.g., "HashLookup").</param>
        /// <param name="key">An object representing the key payload; must not be <c>null</c>.</param>
        /// <returns>A valid Redis key string suitable for storage and lookup.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <c>null</c>.</exception>
        /// <remarks>
        /// For deterministic keys across environments, ensure the <paramref name="key"/> object has stable ordering.
        /// Consider hashing DTOs via a deterministic helper when appropriate.
        /// </remarks>
        public static string GenerateKey(string prefix, object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key), "Key cannot be null");
            }

            // create a cache key for the query and dictionary
            string cacheKey_string = Newtonsoft.Json.JsonConvert.SerializeObject(key);
            // base64 encode the cache key to ensure it is a valid key
            string cacheKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cacheKey_string));

            // return the cache key
            if (!string.IsNullOrEmpty(prefix))
            {
                cacheKey = $"{prefix}:{cacheKey}";
            }

            return cacheKey;
        }

        /// <summary>
        /// Purges all keys across the server for the configured Redis instance.
        /// </summary>
        /// <remarks>
        /// Use cautiously; this deletes every key the server reports, not limited to a specific application prefix.
        /// </remarks>
        public async static Task PurgeCache()
        {
            var server = Connection.GetServer(Config.RedisConfiguration.HostName + ":" + Config.RedisConfiguration.Port);
            List<RedisKey> keyBatch = new List<RedisKey>(PurgeBatchSize);

            foreach (RedisKey key in server.Keys(database: 0))
            {
                keyBatch.Add(key);
                if (keyBatch.Count == PurgeBatchSize)
                {
                    await Db.KeyDeleteAsync(keyBatch.ToArray());
                    keyBatch.Clear();
                }
            }

            if (keyBatch.Count > 0)
            {
                await Db.KeyDeleteAsync(keyBatch.ToArray());
            }
        }

        /// <summary>
        /// Purges keys matching the specified <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">The logical prefix used to namespace keys (e.g., "HashLookup").</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix"/> is <c>null</c> or empty.</exception>
        public async static Task PurgeCache(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentNullException(nameof(prefix), "Prefix cannot be null or empty");
            }

            var shortPrefix = new CacheKey(prefix).Prefix;

            var server = Connection.GetServer(Config.RedisConfiguration.HostName + ":" + Config.RedisConfiguration.Port);
            List<RedisKey> keyBatch = new List<RedisKey>(PurgeBatchSize);

            foreach (RedisKey key in server.Keys(database: 0, pattern: $"{shortPrefix}:*"))
            {
                keyBatch.Add(key);
                if (keyBatch.Count == PurgeBatchSize)
                {
                    await Db.KeyDeleteAsync(keyBatch.ToArray());
                    keyBatch.Clear();
                }
            }

            if (keyBatch.Count > 0)
            {
                await Db.KeyDeleteAsync(keyBatch.ToArray());
            }
        }

        public async static Task DeleteCacheItem(string cacheKey)
        {
            try
            {
                if (!Config.RedisConfiguration.Enabled) return;

                string optimizedKey = new CacheKey(cacheKey).InternalKey;
                await Db.KeyDeleteAsync(optimizedKey);
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "Redis", $"Redis DeleteCacheItem failed for key '{cacheKey}': {ex.Message}", ex);
            }
        }

        #region Cache Helpers
        private static IDatabase Db => RedisConnection.GetDatabase(0);
        private static readonly byte[] CachePayloadMarker = "HRC"u8.ToArray();
        private const byte PlainJsonPayloadFormat = 0;
        private const byte BrotliPayloadFormat = 1;

        private static readonly Newtonsoft.Json.JsonSerializerSettings DeserialiseSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = Newtonsoft.Json.TypeNameAssemblyFormatHandling.Simple,
            MetadataPropertyHandling = Newtonsoft.Json.MetadataPropertyHandling.ReadAhead
        };

        private static readonly Newtonsoft.Json.JsonSerializerSettings SerialiseSettings = new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = Newtonsoft.Json.TypeNameAssemblyFormatHandling.Simple,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            Formatting = Newtonsoft.Json.Formatting.None
        };

        private static bool ShouldSerialize<T>()
        {
            Type type = typeof(T);

            // If it matches any of these primitive types, do NOT serialise/deserialise
            if (type == typeof(string) ||
                type == typeof(int) ||
                type == typeof(long) ||
                type == typeof(bool) ||
                type == typeof(double) ||
                type == typeof(uint) ||
                type == typeof(ulong) ||
                type == typeof(byte[]))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether a cache item exists for the provided <paramref name="cacheKey"/>.
        /// </summary>
        /// <param name="cacheKey">The full Redis key to check.</param>
        /// <returns><c>true</c> if the key exists and Redis is enabled; otherwise <c>false</c>.</returns>
        public async static Task<bool> CacheItemExists(string cacheKey)
        {
            try
            {
                if (Config.RedisConfiguration.Enabled)
                {
                    string shortKey = new CacheKey(cacheKey).InternalKey;
                    return await Db.KeyExistsAsync(shortKey);
                }
                return false;
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "Redis", $"Redis CacheItemExists failed for key '{cacheKey}': {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Retrieves and deserializes a cache item known to be a value type (e.g., <c>bool</c>, <c>int</c>) stored under <paramref name="cacheKey"/>.
        /// </summary>
        /// <typeparam name="T">The struct type of the cached data.</typeparam>
        /// <param name="cacheKey">The full Redis key to read.</param>
        /// <returns>The deserialized value if present; otherwise <c>null</c>, distinguishing a cache miss from a cached default value (e.g., <c>false</c>).</returns>
        /// <remarks>
        /// Use this overload instead of <see cref="GetCacheItem{T}(string)"/> for value types: since <typeparamref name="T"/> is
        /// constrained to <c>struct</c>, the return type is a genuine <see cref="Nullable{T}"/>, so a miss is not ambiguous with a
        /// cached default value.
        /// </remarks>
        public async static Task<T?> GetCacheItemValue<T>(string cacheKey) where T : struct
        {
            try
            {
                if (!Config.RedisConfiguration.Enabled) return null;

                string optimizedKey = new CacheKey(cacheKey).InternalKey;

                if (!ShouldSerialize<T>())
                {
                    RedisValue cachedData = await Db.StringGetAsync(optimizedKey);
                    if (!cachedData.HasValue) return null;

                    string? fallbackString = cachedData.ToString();
                    if (string.IsNullOrEmpty(fallbackString)) return null;
                    return (T)Convert.ChangeType(fallbackString, typeof(T));
                }

                // Lease<byte> rents its backing array from ArrayPool instead of allocating a
                // new array per read, which matters for large cached payloads under load.
                using Lease<byte>? lease = await Db.StringGetLeaseAsync(optimizedKey);
                if (lease == null || lease.Length == 0 || !HasCachePayloadMarker(lease.Span))
                {
                    return null;
                }

                try
                {
                    return DeserializeComplexCacheValue<T>(lease.Memory);
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or Newtonsoft.Json.JsonException)
                {
                    await DeleteInvalidCacheItemAsync(optimizedKey, cacheKey, $"it could not be decoded: {ex.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Log the exception for debugging purposes
                Logging.Log(Logging.LogType.Warning, "Redis", $"Redis GetCacheItemValue<{typeof(T).Name}> failed for key '{cacheKey}': {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Retrieves and deserializes a cache item stored under <paramref name="cacheKey"/>.
        /// </summary>
        /// <typeparam name="T">The expected reference type of the cached data.</typeparam>
        /// <param name="cacheKey">The full Redis key to read.</param>
        /// <returns>The deserialized value if present; otherwise <c>null</c>.</returns>
        /// <remarks>
        /// Uses Newtonsoft.Json with <see cref="Newtonsoft.Json.TypeNameHandling.All"/> to preserve type information.
        /// For value types (e.g., <c>bool</c>, <c>int</c>), use <see cref="GetCacheItemValue{T}(string)"/> instead, since an
        /// unconstrained <c>T?</c> cannot represent "not found" separately from a cached default value.
        /// </remarks>
        public async static Task<T?> GetCacheItem<T>(string cacheKey) where T : class
        {
            try
            {
                if (!Config.RedisConfiguration.Enabled) return default;

                string optimizedKey = new CacheKey(cacheKey).InternalKey;

                if (!ShouldSerialize<T>())
                {
                    RedisValue cachedData = await Db.StringGetAsync(optimizedKey);
                    if (!cachedData.HasValue) return default;

                    string? fallbackString = cachedData.ToString();
                    if (string.IsNullOrEmpty(fallbackString)) return default;
                    return (T)Convert.ChangeType(fallbackString, typeof(T));
                }

                // Lease<byte> rents its backing array from ArrayPool instead of allocating a
                // new array per read, which matters for large cached payloads under load.
                using Lease<byte>? lease = await Db.StringGetLeaseAsync(optimizedKey);
                if (lease == null || lease.Length == 0 || !HasCachePayloadMarker(lease.Span))
                {
                    return default;
                }

                try
                {
                    return DeserializeComplexCacheValue<T>(lease.Memory);
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or Newtonsoft.Json.JsonException)
                {
                    await DeleteInvalidCacheItemAsync(optimizedKey, cacheKey, $"it could not be decoded: {ex.Message}");
                    return default;
                }
            }
            catch (Exception ex)
            {
                // Log the exception for debugging purposes
                Logging.Log(Logging.LogType.Warning, "Redis", $"Redis GetCacheItem<{typeof(T).Name}> failed for key '{cacheKey}': {ex.Message}", ex);
                return default;
            }
        }

        /// <summary>
        /// Serializes and stores a cache item under <paramref name="cacheKey"/> with an optional expiration.
        /// </summary>
        /// <typeparam name="T">The type of the data to cache.</typeparam>
        /// <param name="cacheKey">The full Redis key to write.</param>
        /// <param name="data">The data to serialize and store.</param>
        /// <param name="expiry">Optional time-to-live for the key; if <c>null</c>, the key will expire after 24 hours.</param>
        /// <remarks>
        /// Serialization uses Newtonsoft.Json with <see cref="Newtonsoft.Json.TypeNameHandling.All"/> and ignores nulls.
        /// </remarks>
        public async static Task SetCacheItem<T>(string cacheKey, T? data, TimeSpan? expiry = null)
        {
            try
            {
                if (!Config.RedisConfiguration.Enabled || data == null) return;

                CacheKey key = new CacheKey(cacheKey);
                if (expiry == null)
                {
                    expiry = key.TTL;
                }

                // 1. Primitive routing (Direct Plaintext Write)
                if (!ShouldSerialize<T>())
                {
                    string primitiveData = data?.ToString() ?? string.Empty;
                    await Db.StringSetAsync(key.InternalKey, primitiveData, expiry, false);
                    return;
                }

                await Db.StringSetAsync(key.InternalKey, SerializeComplexCacheValue(data), expiry, false);
            }
            catch (Exception ex)
            {
                // Log the exception for debugging purposes
                Logging.Log(Logging.LogType.Warning, "Redis", $"Redis SetCacheItem<{typeof(T).Name}> failed for key '{cacheKey}': {ex.Message}", ex);
            }
        }
        #endregion Cache Helpers

        #region Compression Helpers
        private static readonly UTF8Encoding Utf8Encoding = new UTF8Encoding(false);

        private static bool HasCachePayloadMarker(ReadOnlySpan<byte> payload)
        {
            return payload.Length > CachePayloadMarker.Length &&
                payload[..CachePayloadMarker.Length].SequenceEqual(CachePayloadMarker);
        }

        internal static bool IsPrimitiveCacheType<T>()
        {
            return !ShouldSerialize<T>();
        }

        /// <summary>
        /// Serializes and frames a value for storage. The raw UTF-8 and (when used) compressed
        /// intermediate buffers are rented from <see cref="ArrayPool{Byte}"/> rather than allocated,
        /// so only the final right-sized payload is a genuine heap allocation.
        /// </summary>
        internal static byte[] SerializeComplexCacheValue<T>(T data)
        {
            string serializedData = Newtonsoft.Json.JsonConvert.SerializeObject(data, SerialiseSettings);

            int rawByteCount = Utf8Encoding.GetByteCount(serializedData);
            byte[] rawBuffer = ArrayPool<byte>.Shared.Rent(rawByteCount);
            try
            {
                int rawLength = Utf8Encoding.GetBytes(serializedData, 0, serializedData.Length, rawBuffer, 0);
                ReadOnlySpan<byte> rawSpan = rawBuffer.AsSpan(0, rawLength);

                if (rawLength > 1000)
                {
                    byte[]? compressedPayload = CreateCompressedCachePayload(rawSpan);
                    if (compressedPayload != null)
                    {
                        return compressedPayload;
                    }
                }

                return CreatePlainJsonCachePayload(rawSpan);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rawBuffer);
            }
        }

        /// <summary>
        /// Deserializes a framed cache payload. Decompression uses a pooled, growable buffer
        /// instead of a <see cref="MemoryStream"/> so large values do not double-buffer on the heap.
        /// </summary>
        internal static T? DeserializeComplexCacheValue<T>(ReadOnlyMemory<byte> payload)
        {
            ReadOnlySpan<byte> span = payload.Span;
            if (!HasCachePayloadMarker(span))
            {
                throw new InvalidDataException("Redis cache payload is unframed.");
            }

            byte format = span[CachePayloadMarker.Length];
            ReadOnlySpan<byte> value = span[(CachePayloadMarker.Length + 1)..];
            string jsonString = format switch
            {
                PlainJsonPayloadFormat => Utf8Encoding.GetString(value),
                BrotliPayloadFormat => DecompressSpanToString(value),
                _ => throw new InvalidDataException($"Unknown Redis cache payload format '{format}'.")
            };

            if (string.IsNullOrEmpty(jsonString))
            {
                throw new InvalidDataException("Redis cache payload is empty.");
            }

            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(jsonString, DeserialiseSettings);
        }

        private static async Task DeleteInvalidCacheItemAsync(string optimizedKey, string cacheKey, string reason)
        {
            try
            {
                bool deleted = await Db.KeyDeleteAsync(optimizedKey);
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Warning, "Redis", $"Could not delete invalid Redis cache item for key '{cacheKey}': {ex.Message}", ex);
            }
        }

        private static byte[] CreatePlainJsonCachePayload(ReadOnlySpan<byte> payload)
        {
            int headerLength = CachePayloadMarker.Length + 1;
            byte[] framedPayload = new byte[headerLength + payload.Length];
            CachePayloadMarker.CopyTo(framedPayload);
            framedPayload[CachePayloadMarker.Length] = PlainJsonPayloadFormat;
            payload.CopyTo(framedPayload.AsSpan(headerLength));
            return framedPayload;
        }

        // Brotli quality 4 / window 22 mirrors the previous BrotliStream(CompressionLevel.Optimal) behavior.
        private const int BrotliQuality = 4;
        private const int BrotliWindow = 22;

        private static byte[]? CreateCompressedCachePayload(ReadOnlySpan<byte> rawSpan)
        {
            int headerLength = CachePayloadMarker.Length + 1;
            int maxCompressedLength = BrotliEncoder.GetMaxCompressedLength(rawSpan.Length);
            byte[] rented = ArrayPool<byte>.Shared.Rent(headerLength + maxCompressedLength);
            try
            {
                if (!BrotliEncoder.TryCompress(rawSpan, rented.AsSpan(headerLength), out int written, BrotliQuality, BrotliWindow))
                {
                    return null;
                }

                if ((double)written / rawSpan.Length > 0.80)
                {
                    return null;
                }

                int totalLength = headerLength + written;
                byte[] framedPayload = new byte[totalLength];
                CachePayloadMarker.CopyTo(framedPayload);
                framedPayload[CachePayloadMarker.Length] = BrotliPayloadFormat;
                rented.AsSpan(headerLength, written).CopyTo(framedPayload.AsSpan(headerLength));
                return framedPayload;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        // Decompresses into a pooled, doubling buffer instead of a growable MemoryStream so large
        // payloads don't hold two large buffers (stream + ToArray copy) on the heap at once.
        private static string DecompressSpanToString(ReadOnlySpan<byte> compressedBytes)
        {
            const int maxBufferSize = 512 * 1024 * 1024; // safety cap
            int bufferSize = Math.Clamp(compressedBytes.Length * 4, 4096, maxBufferSize);

            while (true)
            {
                byte[] rented = ArrayPool<byte>.Shared.Rent(bufferSize);
                try
                {
                    if (BrotliDecoder.TryDecompress(compressedBytes, rented, out int written))
                    {
                        return Utf8Encoding.GetString(rented, 0, written);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }

                if (bufferSize >= maxBufferSize)
                {
                    throw new InvalidDataException("Redis cache payload exceeded maximum decompression size.");
                }

                bufferSize = (int)Math.Min((long)bufferSize * 2, maxBufferSize);
            }
        }

        /// <summary>
        /// Compresses a plain text string into a Brotli-compressed binary byte array.
        /// </summary>
        /// <param name="text">The raw text payload (e.g., JSON string) to compress.</param>
        /// <returns>A compressed byte array.</returns>
        public static byte[] CompressString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<byte>();

            byte[] rawBytes = Utf8Encoding.GetBytes(text);
            int maxCompressedLength = BrotliEncoder.GetMaxCompressedLength(rawBytes.Length);
            byte[] rented = ArrayPool<byte>.Shared.Rent(maxCompressedLength);
            try
            {
                if (!BrotliEncoder.TryCompress(rawBytes, rented, out int written, BrotliQuality, BrotliWindow))
                {
                    throw new InvalidOperationException("Brotli compression failed.");
                }

                return rented.AsSpan(0, written).ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Decompresses a Brotli-compressed binary byte array back into a plain text string.
        /// </summary>
        /// <param name="compressedBytes">The binary payload retrieved from the cache store.</param>
        /// <returns>The original uncompressed plain text string.</returns>
        public static string DecompressToString(byte[] compressedBytes)
        {
            if (compressedBytes == null || compressedBytes.Length == 0)
                return string.Empty;

            return DecompressSpanToString(compressedBytes);
        }

        #endregion Compression Helpers

        #region Cache Policies
        private readonly static Dictionary<string, CachePolicy> CachePolicies = new Dictionary<string, CachePolicy>
        {
            { "AttributeItems", new CachePolicy { ShortName = "ai", TTL = TimeSpan.FromMinutes(5) } },
            { "DataObject", new CachePolicy { ShortName = "do", TTL = TimeSpan.FromHours(1) } },
            { "DataObjectFromSignatureId", new CachePolicy { ShortName = "ds", TTL = TimeSpan.FromHours(1) } },
            { "GameItem", new CachePolicy { ShortName = "gi", TTL = TimeSpan.FromHours(1) } },
            { "Lookup", new CachePolicy { ShortName = "lu", TTL = TimeSpan.FromHours(1) } },
            { "HashLookup", new CachePolicy { ShortName = "hl", TTL = TimeSpan.FromHours(1) } },
            { "InsightsReport", new CachePolicy { ShortName = "ir", TTL = TimeSpan.FromHours(1) } },
            { "Insights", new CachePolicy { ShortName = "in", TTL = TimeSpan.FromHours(1) } },
            { "MetadataItem", new CachePolicy { ShortName = "mi", TTL = TimeSpan.FromHours(1) } },
            { "RomItem", new CachePolicy { ShortName = "ri", TTL = TimeSpan.FromHours(1) } },
            { "Signature", new CachePolicy { ShortName = "sg", TTL = TimeSpan.FromHours(1) } }
        };

        private class CachePolicy
        {
            public string ShortName { get; set; } = string.Empty;
            public TimeSpan TTL { get; set; } = TimeSpan.Zero;
        }

        private class CacheKey
        {
            public CacheKey()
            { }
            public CacheKey(string cacheKey)
            {
                this.OriginalKey = cacheKey;

                // split the cache key into prefix and key
                int separatorIndex = cacheKey.IndexOf(':');
                var longPrefix = separatorIndex > 0 ? cacheKey.Substring(0, separatorIndex) : string.Empty;
                var remainingKey = separatorIndex > 0 ? cacheKey.Substring(separatorIndex + 1) : cacheKey;

                if (!string.IsNullOrEmpty(longPrefix) && CachePolicies.TryGetValue(longPrefix, out CachePolicy? policy))
                {
                    this.TTL = policy.TTL;
                    this.Prefix = policy.ShortName;
                }
                else
                {
                    this.TTL = TimeSpan.FromMinutes(5); // default TTL
                    this.Prefix = longPrefix;
                }

                if (remainingKey.Length <= 32)
                {
                    this.InternalKey = $"{this.Prefix}:{remainingKey}";
                }
                else
                {
                    byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(remainingKey);
                    string keyHash = Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(inputBytes));

                    this.InternalKey = $"{this.Prefix}:{keyHash}";
                }
            }

            public string OriginalKey { get; set; } = string.Empty;
            public string InternalKey { get; set; } = string.Empty;
            public string Prefix { get; set; } = string.Empty;
            public TimeSpan TTL { get; set; } = TimeSpan.Zero;
        }
    }
    #endregion Cache Policies
}