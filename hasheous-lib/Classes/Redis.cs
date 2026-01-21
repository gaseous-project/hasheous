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

        /// <summary>
        /// Purges all keys across the server for the configured Redis instance.
        /// </summary>
        /// <remarks>
        /// Use cautiously; this deletes every key the server reports, not limited to a specific application prefix.
        /// </remarks>
        public static void PurgeCache()
        {
            var server = Connection.GetServer(Config.RedisConfiguration.HostName + ":" + Config.RedisConfiguration.Port);
            var keys = server.Keys();

            foreach (var key in keys)
            {
                GetDatabase(0).KeyDelete(key);
            }
        }

        /// <summary>
        /// Purges keys matching the specified <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">The logical prefix used to namespace keys (e.g., "HashLookup").</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix"/> is <c>null</c> or empty.</exception>
        public static void PurgeCache(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentNullException(nameof(prefix), "Prefix cannot be null or empty");
            }

            var server = Connection.GetServer(Config.RedisConfiguration.HostName + ":" + Config.RedisConfiguration.Port);
            var keys = server.Keys(pattern: $"{prefix}:*");

            foreach (var key in keys)
            {
                GetDatabase(0).KeyDelete(key);
            }
        }

        /// <summary>
        /// Checks whether a cache item exists for the provided <paramref name="cacheKey"/>.
        /// </summary>
        /// <param name="cacheKey">The full Redis key to check.</param>
        /// <returns><c>true</c> if the key exists and Redis is enabled; otherwise <c>false</c>.</returns>
        public static bool CacheItemExists(string cacheKey)
        {
            if (Config.RedisConfiguration.Enabled)
            {
                return RedisConnection.GetDatabase(0).KeyExists(cacheKey);
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
        public static T? GetCacheItem<T>(string cacheKey)
        {
            // check redis cache first
            if (Config.RedisConfiguration.Enabled)
            {
                if (RedisConnection.GetDatabase(0).KeyExists(cacheKey))
                {
                    string? cachedData = RedisConnection.GetDatabase(0).StringGet(cacheKey);
                    if (cachedData != null)
                    {
                        // if cached data is found, deserialize it and return
                        var settings = new Newtonsoft.Json.JsonSerializerSettings
                        {
                            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
                            TypeNameAssemblyFormatHandling = Newtonsoft.Json.TypeNameAssemblyFormatHandling.Simple
                        };
                        var deserializedData = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(cachedData, settings);
                        if (deserializedData != null)
                        {
                            return deserializedData;
                        }
                    }
                }
            }
            return default(T);
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
        public static void SetCacheItem<T>(string cacheKey, T data, TimeSpan? expiry = null)
        {
            if (Config.RedisConfiguration.Enabled)
            {
                var settings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
                };
                string serializedData = Newtonsoft.Json.JsonConvert.SerializeObject(data, settings);
                RedisConnection.GetDatabase(0).StringSet(cacheKey, serializedData, expiry);
            }
        }
    }
}