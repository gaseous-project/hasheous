using Classes;
using StackExchange.Redis;

namespace hasheous.Classes
{
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

        public static ConnectionMultiplexer Connection => lazyConnection.Value;

        public static IDatabase GetDatabase(int db = -1)
        {
            return Connection.GetDatabase(db);
        }

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

        public static void PurgeCache()
        {
            var server = Connection.GetServer(Config.RedisConfiguration.HostName + ":" + Config.RedisConfiguration.Port);
            var keys = server.Keys();

            foreach (var key in keys)
            {
                GetDatabase(0).KeyDelete(key);
            }
        }

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
    }
}