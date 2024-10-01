namespace Classes
{
    /// <summary>
    /// A simple cache for storing key-value pairs
    /// </summary>
    /// <remarks>
    /// This cache is not thread-safe and should not be used in a multi-threaded environment
    /// </remarks>
    /// <example>
    /// <code>
    /// LookupCache.Add("myCache", "myKey", "myValue", 60);
    /// string value = LookupCache.Get("myCache", "myKey");
    /// </code>
    /// </example>
    /// 
    public class LookupCache
    {
        private static Dictionary<string, LookupCacheRoot> Cache = new Dictionary<string, LookupCacheRoot>();

        /// <summary>
        /// Add a key-value pair to the cache
        /// </summary>
        /// <param name="root">
        /// The root of the cache, used to separate different caches
        /// </param>
        /// <param name="key">
        /// The key to store the value under
        /// </param>
        /// <param name="value">
        /// The value to store
        /// </param>
        /// <param name="ttl">
        /// The time-to-live of the cache in seconds
        /// </param>
        public static void Add(string root, string key, string value, int ttl = 60)
        {
            if (!Cache.ContainsKey(root))
            {
                Cache.Add(root, new LookupCacheRoot());
            }
            Cache[root].Add(key, new LookupCacheRoot.LookupCacheItem
            {
                Value = value,
                TTL = ttl
            });
        }

        /// <summary>
        /// Get a value from the cache
        /// </summary>
        /// <param name="root">
        /// The root of the cache, used to separate different caches
        /// </param>
        /// <param name="key">
        /// The key to get the value for
        /// </param>
        /// <returns>
        /// The value stored in the cache, or null if the key does not exist or has expired
        /// </returns>
        public static string? Get(string root, string key)
        {
            if (Cache.ContainsKey(root))
            {
                if (Cache[root].ContainsKey(key))
                {
                    try
                    {
                        return Cache[root].Get(key).Value;
                    }
                    catch (KeyNotFoundException)
                    {
                        return null;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Remove a key from the cache
        /// </summary>
        /// <param name="root">
        /// The root of the cache, used to separate different caches
        /// </param>
        /// <param name="key">
        /// The key to remove
        /// </param>
        public static void Remove(string key)
        {
            Cache.Remove(key);
        }
    }

    public class LookupCacheRoot
    {
        public static Dictionary<string, LookupCacheItem> Cache = new Dictionary<string, LookupCacheItem>();

        internal void Add(string key, LookupCacheItem lookupCacheItem)
        {
            if (Cache.ContainsKey(key))
            {
                Cache.Remove(key);
            }

            Cache.Add(key, lookupCacheItem);
        }

        internal bool ContainsKey(string key)
        {
            return Cache.ContainsKey(key);
        }

        internal void Remove(string key)
        {
            if (Cache.ContainsKey(key))
            {
                Cache.Remove(key);
            }
        }

        internal LookupCacheItem Get(string key)
        {
            if (!Cache.ContainsKey(key))
            {
                throw new KeyNotFoundException();
            }

            if (Cache[key].ExpiryTime < DateTime.UtcNow)
            {
                Cache.Remove(key);
                throw new KeyNotFoundException();
            }

            // extend the lifetime of the lookup
            Cache[key].LastUsedTime = DateTime.UtcNow;

            return Cache[key];
        }

        public class LookupCacheItem
        {
            public string Value { get; set; }
            private DateTime _addedTime = DateTime.UtcNow;
            public DateTime AddedTime
            {
                get
                {
                    return _addedTime;
                }
            }
            private DateTime _lastUsedTime = DateTime.UtcNow;
            public DateTime LastUsedTime
            {
                get
                {
                    return _lastUsedTime;
                }
                set
                {
                    _lastUsedTime = value;
                }
            }
            public int TTL { get; set; }
            public DateTime ExpiryTime
            {
                get
                {
                    if (LastUsedTime > AddedTime)
                    {
                        return LastUsedTime.AddSeconds(TTL);
                    }
                    else
                    {
                        return AddedTime.AddSeconds(TTL);
                    }
                }
            }
        }
    }
}