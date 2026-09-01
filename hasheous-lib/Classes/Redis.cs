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
        public static ConnectionMultiplexer Connection => lazyConnection.Value;

        /// <summary>
        /// Retrieves an <see cref="IDatabase"/> reference for the given logical database index.
        /// </summary>
        /// <param name="db">The Redis logical database number. Use <c>-1</c> to select the default database.</param>
        /// <returns>An <see cref="IDatabase"/> for executing Redis commands.</returns>
        public static IDatabase GetDatabase(int db = -1)
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

            // convert the rest of the cache key to md5 hash to reduce the length of the key
            string keyHash = "";
            if (remainingKey.Length <= 32)
            {
                keyHash = remainingKey;
            }
            else
            {
                // Use MD5 to hash the remaining key for a consistent length
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(remainingKey);
                    byte[] hashBytes = md5.ComputeHash(inputBytes);
                    keyHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }

                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(remainingKey);
                    byte[] hashBytes = md5.ComputeHash(inputBytes);

                    // OPTIMISATION: Fast hex generation bypasses string.Replace and .ToLower allocations completely
                    var hexBuilder = new StringBuilder(32);
                    for (int i = 0; i < hashBytes.Length; i++)
                    {
                        hexBuilder.Append(hashBytes[i].ToString("x2"));
                    }

                    keyHash = hexBuilder.ToString();
                }
            }

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
                    case "lookup":
                        return "lu";
                    case "insightsreport":
                        return "ir";
                    case "romitem":
                        return "ri";
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
            var keys = server.Keys();

            foreach (var key in keys)
            {
                await GetDatabase(0).KeyDeleteAsync(key);
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
            var keys = server.Keys(pattern: $"{shortPrefix}:*");

            foreach (var key in keys)
            {
                await GetDatabase(0).KeyDeleteAsync(key);
            }
        }

        #region Cache Helpers
        private static IDatabase Db => RedisConnection.GetDatabase(0);

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
            if (Config.RedisConfiguration.Enabled)
            {
                string shortKey = GenerateInternalKey(cacheKey);
                return await Db.KeyExistsAsync(shortKey);
            }
            return false;
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

                RedisValue cachedData = await Db.StringGetAsync(optimizedKey);
                if (!cachedData.HasValue) return default;

                if (!ShouldSerialize<T>())
                {
                    string? fallbackString = cachedData.ToString();
                    if (string.IsNullOrEmpty(fallbackString)) return default;
                    return (T)Convert.ChangeType(fallbackString, typeof(T));
                }

                string jsonString;

                // FIX: Safely retrieve the absolute raw underlying bytes from StackExchange.Redis
                // without allowing the driver to perform implicit character encoding coercions.
                byte[] rawBuffer = cachedData;

                if (rawBuffer == null || rawBuffer.Length == 0) return default;

                // Direct check: If it starts with '{' (0x7B) or '[' (0x5B), it is DEFINITELY raw uncompressed text JSON
                if (rawBuffer[0] == 0x7B || rawBuffer[0] == 0x5B)
                {
                    jsonString = Utf8Encoding.GetString(rawBuffer);
                }
                else
                {
                    // It's binary payload -> Process through Brotli pipeline safely
                    try
                    {
                        using var inputStream = new MemoryStream(rawBuffer);
                        using var decompressStream = new BrotliStream(inputStream, CompressionMode.Decompress);
                        using var outputStream = new MemoryStream();

                        await decompressStream.CopyToAsync(outputStream);
                        jsonString = Utf8Encoding.GetString(outputStream.ToArray());
                    }
                    catch (Exception)
                    {
                        // Fail-safe fallback: If decompression crashes, fall back to string interpretation
                        jsonString = cachedData.ToString()!;
                    }
                }

                if (string.IsNullOrEmpty(jsonString)) return default;
                return (T?)Newtonsoft.Json.JsonConvert.DeserializeObject(jsonString, DeserialiseSettings);
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
        /// <param name="expiry">Optional time-to-live for the key; if <c>null</c>, the key does not expire.</param>
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

                // 2. Complex Object Serialization
                string serializedData = Newtonsoft.Json.JsonConvert.SerializeObject(data, SerialiseSettings);

                // THRESHOLD RULE A: Check if character count exceeds 1000
                if (serializedData.Length > 1000)
                {
                    byte[] compressedBytes = CompressString(serializedData);

                    // THRESHOLD RULE B: Check if compressed byte array is more than 20% smaller than raw text characters
                    double savingRatio = (double)compressedBytes.Length / serializedData.Length;
                    if (savingRatio <= 0.80)
                    {
                        // Storing a byte[] array forces Valkey to flag this key as a binary stream natively
                        await Db.StringSetAsync(shortKey, compressedBytes, expiry, false);
                        return;
                    }
                }

                // Fallback: If it fails character count or saving threshold, store as plain JSON string
                await Db.StringSetAsync(shortKey, serializedData, expiry, false);
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