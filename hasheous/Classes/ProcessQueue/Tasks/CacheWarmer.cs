using hasheous.Classes;
using hasheous_server.Classes;
using hasheous_server.Models;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that warms up the cache.
    /// This task is intended to perform any necessary preloading of data into memory or other preparatory tasks.
    /// </summary>
    public class CacheWarmer : IQueueTask
    {
        /// <inheritdoc/>
        public string TaskName { get; set; } = "CacheWarmer";

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            if (Config.RedisConfiguration.Enabled)
            {
                // warm all app reports
                string cacheKey = RedisConnection.GenerateKey("InsightsReport", 0);

                // delete existing cache entry if it exists
                if (RedisConnection.GetDatabase(0).KeyExists(cacheKey))
                {
                    RedisConnection.GetDatabase(0).KeyDelete(cacheKey);
                }

                // generate the report and cache it
                _ = await Insights.Insights.GenerateInsightReport(0);

                hasheous_server.Classes.DataObjects dataObjects = new hasheous_server.Classes.DataObjects();

                // warm per app reports
                DataObjectsList dataObjectsList = await dataObjects.GetDataObjects(DataObjects.DataObjectType.App);

                foreach (DataObjectItem item in dataObjectsList.Objects)
                {
                    cacheKey = RedisConnection.GenerateKey("InsightsReport", item.Id);

                    // delete existing cache entry if it exists
                    if (RedisConnection.GetDatabase(0).KeyExists(cacheKey))
                    {
                        RedisConnection.GetDatabase(0).KeyDelete(cacheKey);
                    }

                    // generate the report and cache it
                    _ = await Insights.Insights.GenerateInsightReport(item.Id);
                }
            }

            return null; // Assuming the method returns void, we return null here.
        }
    }
}