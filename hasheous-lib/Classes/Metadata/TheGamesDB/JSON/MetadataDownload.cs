using System.Net;
using Classes;

namespace TheGamesDB.JSON
{
    public class DownloadManager
    {
        public string Url
        {
            get
            {
                return "https://cdn.thegamesdb.net/json/database-latest.json";
            }
        }

        public string LocalFilePath
        {
            get
            {
                return Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_TheGamesDb);
            }
        }

        public string LocalFileName
        {
            get
            {
                return Path.Combine(LocalFilePath, "database-latest.json");
            }
        }

        public int MaxAgeInDays { get; set; } = 30;

        public bool IsLocalCopyOlderThanMaxAge()
        {
            if (!File.Exists(LocalFileName))
            {
                return true;
            }

            var lastWriteTime = File.GetLastWriteTime(LocalFileName);
            var age = DateTime.Now - lastWriteTime;
            return age.TotalDays > MaxAgeInDays;
        }

        public async Task<string> Download()
        {
            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            if (IsLocalCopyOlderThanMaxAge() == true)
            {
                Logging.Log(Logging.LogType.Information, "TheGamesDb", "Downloading metadata database from TheGamesDb");
                using (var client = new WebClient())
                {
                    var json = await client.DownloadStringTaskAsync(new Uri(Url));
                    await File.WriteAllTextAsync(LocalFileName, json);
                }
            }
            else
            {
                Logging.Log(Logging.LogType.Information, "TheGamesDb", "Using local copy of metadata database");
            }

            return LocalFileName;
        }
    }
}