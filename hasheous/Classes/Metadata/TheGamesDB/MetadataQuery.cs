using Classes;
using Newtonsoft.Json;

namespace TheGamesDB
{
    public class MetadataQuery
    {
        private static DateTime _lastQuery = DateTime.UtcNow;
        private static TheGamesDBDatabase _metadata = null;
        public static TheGamesDBDatabase metadata
        {
            get
            {
                DownloadManager downloadManager = new DownloadManager();

                if (downloadManager.IsLocalCopyOlderThanMaxAge() == true || _metadata == null)
                {
                    downloadManager.Download();

                    // string json = File.ReadAllText(downloadManager.LocalFileName);
                    // _metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                    using (StreamReader file = File.OpenText(downloadManager.LocalFileName))
                    {
                        using (JsonReader reader = new JsonTextReader(file))
                        {
                            JsonSerializer serializer = new JsonSerializer();

                            _metadata = serializer.Deserialize<TheGamesDBDatabase>(reader);
                        }
                    }
                }

                _lastQuery = DateTime.UtcNow;

                return _metadata;
            }
        }

        public static void RunMaintenance()
        {
            if (_lastQuery.AddMinutes(45) < DateTime.UtcNow)
            {
                Logging.Log(Logging.LogType.Information, "TheGamesDB", "Running maintenance on metadata cache");
                _metadata = null;
            }
        }
    }
}