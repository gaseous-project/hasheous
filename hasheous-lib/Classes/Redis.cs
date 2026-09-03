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

        private static string GenerateInternalKey(string cacheKey)
        {
            // split the cache key into prefix and key
            int separatorIndex = cacheKey.IndexOf(':');
            var longPrefix = separatorIndex > 0 ? cacheKey.Substring(0, separatorIndex) : string.Empty;
            var remainingKey = separatorIndex > 0 ? cacheKey.Substring(separatorIndex + 1) : cacheKey;
            string shortPrefix = GetShortPrefix(longPrefix);

            if (remainingKey.Length <= 32)
            {
                return $"{shortPrefix}:{remainingKey}";
            }

            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(remainingKey);
            string keyHash = Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(inputBytes));

            return $"{shortPrefix}:{keyHash}";
        }

        private static string GetShortPrefix(string longPrefix)
        {
            if (longPrefix.Length > 2)
            {
                switch (longPrefix.ToLower())
                {
                    case "dataobject":
                        return "do";
                    case "dataobjectfromsignatureid":
                        return "ds";
                    case "gameitem":
                        return "gi";
                    case "lookup":
                        return "lu";
                    case "hashlookup":
                        return "hl";
                    case "insightsreport":
                        return "ir";
                    case "insights":
                        return "in";
                    case "romitem":
                        return "ri";
                    case "signature":
                        return "sg";
                    default:
                        return longPrefix;
                }
            }
            return longPrefix;
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

            var shortPrefix = GetShortPrefix(prefix);

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

                string optimizedKey = GenerateInternalKey(cacheKey);
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
                    string shortKey = GenerateInternalKey(cacheKey);
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
        /// Retrieves and deserializes a cache item stored under <paramref name="cacheKey"/>.
        /// </summary>
        /// <typeparam name="T">The expected type of the cached data.</typeparam>
        /// <param name="cacheKey">The full Redis key to read.</param>
        /// <returns>The deserialized value if present; otherwise <c>default(T)</c>.</returns>
        /// <remarks>
        /// Uses Newtonsoft.Json with <see cref="Newtonsoft.Json.TypeNameHandling.All"/> to preserve type information.
        /// </remarks>
        public async static Task<T?> GetCacheItem<T>(string cacheKey)
        {
            try
            {
                if (!Config.RedisConfiguration.Enabled) return default;

                string optimizedKey = GenerateInternalKey(cacheKey);

                RedisValue? cachedData = await Db.StringGetAsync(optimizedKey);
                if (!cachedData.HasValue) return default;

                if (!ShouldSerialize<T>())
                {
                    string? fallbackString = cachedData.ToString();
                    if (string.IsNullOrEmpty(fallbackString)) return default;
                    return (T)Convert.ChangeType(fallbackString, typeof(T));
                }

                byte[]? rawBuffer = cachedData;
                if (rawBuffer == null || rawBuffer.Length == 0 || !HasCachePayloadMarker(rawBuffer))
                {
                    await DeleteInvalidCacheItemAsync(optimizedKey, cacheKey, "it is unframed or empty");
                    return default;
                }

                try
                {
                    return await DeserializeComplexCacheValue<T>(rawBuffer);
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
                if (expiry == null || expiry > TimeSpan.FromHours(24))
                {
                    expiry = TimeSpan.FromHours(24);
                }

                if (!Config.RedisConfiguration.Enabled || data == null) return;

                string shortKey = GenerateInternalKey(cacheKey);

                // 1. Primitive routing (Direct Plaintext Write)
                if (!ShouldSerialize<T>())
                {
                    string primitiveData = data?.ToString() ?? string.Empty;
                    await Db.StringSetAsync(shortKey, primitiveData, expiry, false);
                    return;
                }

                await Db.StringSetAsync(shortKey, SerializeComplexCacheValue(data), expiry, false);
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

        private static bool HasCachePayloadMarker(byte[] payload)
        {
            return payload.Length > CachePayloadMarker.Length &&
                payload.AsSpan(0, CachePayloadMarker.Length).SequenceEqual(CachePayloadMarker);
        }

        internal static bool IsPrimitiveCacheType<T>()
        {
            return !ShouldSerialize<T>();
        }

        internal static byte[] SerializeComplexCacheValue<T>(T data)
        {
            string serializedData = Newtonsoft.Json.JsonConvert.SerializeObject(data, SerialiseSettings);

            if (serializedData.Length > 1000)
            {
                byte[]? compressedPayload = CreateCompressedCachePayload(serializedData);
                if (compressedPayload != null)
                {
                    return compressedPayload;
                }
            }

            return CreatePlainJsonCachePayload(serializedData);
        }

        internal static async Task<T?> DeserializeComplexCacheValue<T>(byte[] payload)
        {
            if (!HasCachePayloadMarker(payload))
            {
                throw new InvalidDataException("Redis cache payload is unframed.");
            }

            byte format = payload[CachePayloadMarker.Length];
            byte[] value = payload[(CachePayloadMarker.Length + 1)..];
            string jsonString = format switch
            {
                PlainJsonPayloadFormat => Utf8Encoding.GetString(value),
                BrotliPayloadFormat => await DecompressToStringAsync(value),
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

        private static byte[] CreateCachePayload(byte format, byte[] payload)
        {
            byte[] framedPayload = new byte[CachePayloadMarker.Length + 1 + payload.Length];
            CachePayloadMarker.CopyTo(framedPayload, 0);
            framedPayload[CachePayloadMarker.Length] = format;
            payload.CopyTo(framedPayload, CachePayloadMarker.Length + 1);
            return framedPayload;
        }

        private static byte[] CreatePlainJsonCachePayload(string payload)
        {
            int headerLength = CachePayloadMarker.Length + 1;
            byte[] framedPayload = new byte[headerLength + Utf8Encoding.GetByteCount(payload)];
            CachePayloadMarker.CopyTo(framedPayload, 0);
            framedPayload[CachePayloadMarker.Length] = PlainJsonPayloadFormat;
            Utf8Encoding.GetBytes(payload, 0, payload.Length, framedPayload, headerLength);
            return framedPayload;
        }

        private static byte[]? CreateCompressedCachePayload(string payload)
        {
            byte[] rawBytes = Utf8Encoding.GetBytes(payload);
            using var outputStream = new MemoryStream();
            outputStream.Write(CachePayloadMarker);
            outputStream.WriteByte(BrotliPayloadFormat);

            using (var compressStream = new BrotliStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                compressStream.Write(rawBytes, 0, rawBytes.Length);
            }

            int compressedLength = checked((int)outputStream.Length - CachePayloadMarker.Length - 1);
            return (double)compressedLength / payload.Length <= 0.80 ? outputStream.ToArray() : null;
        }

        private static async Task<string> DecompressToStringAsync(byte[] compressedBytes)
        {
            using var inputStream = new MemoryStream(compressedBytes);
            using var decompressStream = new BrotliStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();
            await decompressStream.CopyToAsync(outputStream);
            return Utf8Encoding.GetString(outputStream.ToArray());
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

            using var outputStream = new MemoryStream();
            // CompressionLevel.Optimal delivers the best compression ratio, suitable for cache layers
            using (var compressStream = new BrotliStream(outputStream, CompressionLevel.Optimal))
            {
                compressStream.Write(rawBytes, 0, rawBytes.Length);
            }

            return outputStream.ToArray();
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

            using var inputStream = new MemoryStream(compressedBytes);
            using var decompressStream = new BrotliStream(inputStream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream();

            decompressStream.CopyTo(outputStream);

            return Utf8Encoding.GetString(outputStream.ToArray());
        }

        #endregion Compression Helpers
    }
}