using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using Classes;
using HasheousClient.Models.Metadata.IGDB;
using NuGet.Common;
using RetroAchievements.Models;

namespace RetroAchievements
{
    public class DownloadManager
    {
        private static readonly HttpClient client = new HttpClient();

        public string PlatformsUrl
        {
            get
            {
                return $"https://retroachievements.org/API/API_GetConsoleIDs.php?y={Config.RetroAchievements.APIKey}&g=1";
            }
        }

        public string PlatformGamesUrl
        {
            get
            {
                return $"https://retroachievements.org/API/API_GetGameList.php?y={Config.RetroAchievements.APIKey}&i=<PlatformID>&h=1&c=<PageSize>&o=<PageOffset>";
            }
        }

        public string PlatformGamesHashesUrl
        {
            get
            {
                return $"https://retroachievements.org/API/API_GetGameHashes.php?y={Config.RetroAchievements.APIKey}&i=<GameID>";
            }
        }

        public string LocalFilePath
        {
            get
            {
                return Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_RetroAchievements);
            }
        }

        public string PlatformsLocalFileName
        {
            get
            {
                return Path.Combine(LocalFilePath, "platforms.json");
            }
        }

        public int MaxAgeInDays { get; set; } = 90;

        public bool IsLocalCopyOlderThanMaxAge(string LocalFileName)
        {
            if (!File.Exists(LocalFileName))
            {
                return true;
            }

            var lastWriteTime = File.GetLastWriteTime(LocalFileName);
            var age = DateTime.Now - lastWriteTime;
            return age.TotalDays > MaxAgeInDays;
        }

        public async Task Download()
        {
            // download the platforms file
            _Download(PlatformsUrl, PlatformsLocalFileName, "Platforms");

            // load the platforms file into memory
            List<Models.PlatformModel> platforms = new List<Models.PlatformModel>();
            if (File.Exists(PlatformsLocalFileName))
            {
                string json = File.ReadAllText(PlatformsLocalFileName);
                platforms = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Models.PlatformModel>>(json);
            }

            // setup the DAT output directory
            string datPath = Path.Combine(LocalFilePath, "DATS");
            if (!Directory.Exists(datPath))
            {
                Directory.CreateDirectory(datPath);
            }

            // download the games for each platform
            int platformCount = 0;
            foreach (var platform in platforms)
            {
                platformCount++;
                platform.Games = new List<Models.GameModel>();
                Logging.Log(Logging.LogType.Information, "RetroAchievements", $"Downloading games for platform {platformCount} of {platforms.Count}");

                // download the games files setting the page size to 1000 and starting with a page offset of 0. Keep downloading until we get an empty response
                int pageSize = 500;
                int pageOffset = 0;
                bool keepDownloading = true;
                while (keepDownloading)
                {
                    string url = PlatformGamesUrl.Replace("<PlatformID>", platform.ID.ToString()).Replace("<PageSize>", pageSize.ToString()).Replace("<PageOffset>", pageOffset.ToString());
                    string localGamePath = Path.Combine(LocalFilePath, platform.ID.ToString());
                    if (!Directory.Exists(localGamePath))
                    {
                        Directory.CreateDirectory(localGamePath);
                    }
                    string localFileName = Path.Combine(localGamePath, pageOffset + ".json");

                    // download the games file
                    _Download(url, localFileName, "Games");

                    // load the games file into memory
                    List<Models.GameModel> games = new List<GameModel>();
                    if (File.Exists(localFileName))
                    {
                        string json = File.ReadAllText(localFileName);
                        games = Newtonsoft.Json.JsonConvert.DeserializeObject<List<GameModel>>(json);
                    }

                    // if we got an empty response, stop downloading
                    if (games.Count == 0)
                    {
                        keepDownloading = false;
                    }
                    else
                    {
                        // download the game hashes for each game
                        int gameCount = 0;
                        foreach (var game in games)
                        {
                            gameCount++;
                            // Logging.Log(Logging.LogType.Information, "RetroAchievements", $"Downloading game hashes for game {gameCount} of {games.Count}");

                            string gameHashesUrl = PlatformGamesHashesUrl.Replace("<GameID>", game.ID.ToString());
                            string localGameHashesPath = Path.Combine(localGamePath, "Hashes");
                            if (!Directory.Exists(localGameHashesPath))
                            {
                                Directory.CreateDirectory(localGameHashesPath);
                            }
                            string localGameHashesFileName = Path.Combine(localGameHashesPath, game.ID + ".json");

                            // download the game hashes file if it doesn't exist, or game.DateModified is in the last max age days
                            if (!File.Exists(localGameHashesFileName) || game.DateModified > DateTime.Now.AddDays(-MaxAgeInDays))
                            {
                                _Download(gameHashesUrl, localGameHashesFileName, "GameHashes");
                            }
                            else
                            {
                                // Logging.Log(Logging.LogType.Information, "RetroAchievements", $"Game hashes for game {game.ID} are up to date.");
                            }

                            // load the game hashes file into memory
                            if (File.Exists(localGameHashesFileName))
                            {
                                string json = File.ReadAllText(localGameHashesFileName);
                                Dictionary<string, List<GameHashesModel>> gameHashes = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, List<GameHashesModel>>>(json);
                                game.GameHashes = gameHashes["Results"];
                            }

                            // add the game to the platform
                            platform.Games.Add(game);
                        }
                    }

                    // increment the page offset
                    pageOffset += pageSize;
                }

                // wait an extra 5 seconds before downloading the next platform
                Thread.Sleep(5000);

                // compile the DAT XML file for the platform
                CompileXMLDatResponse? datFile = CompileXMLDat(platform, datPath);
            }

            // copy the DAT files to the processing directory
            string signatureDestDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "RetroAchievements");
            if (Directory.Exists(signatureDestDir))
            {
                Directory.Delete(signatureDestDir, true);
            }
            Directory.CreateDirectory(signatureDestDir);
            foreach (string file in Directory.GetFiles(datPath, "*.dat", SearchOption.TopDirectoryOnly))
            {
                string destFile = Path.Combine(signatureDestDir, Path.GetFileName(file));
                File.Copy(file, destFile);

                Logging.Log(Logging.LogType.Information, "RetroAchievements", $"RetroAchievements metadata file copied to processing directory: {destFile}");
            }

            // force start the signature ingest process
            foreach (var process in Classes.ProcessQueue.QueueProcessor.QueueItems)
            {
                if (process.ItemType == Classes.ProcessQueue.QueueItemType.SignatureIngestor)
                {
                    process.ForceExecute();
                }
            }

            return;
        }

        private Dictionary<string, string> _Download(string Url, string DestinationFile, string DataType)
        {
            Dictionary<string, string> results = new Dictionary<string, string>();

            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            // populate output
            results.Add("Url", Url);
            results.Add("DestinationFile", DestinationFile);

            // download the platforms file
            if (IsLocalCopyOlderThanMaxAge(DestinationFile) == true)
            {
                // Logging.Log(Logging.LogType.Information, "RetroAchievements", $"Downloading {DataType} metadata database from RetroAchievements");

                // download the file
                var result = DownloadFile(Url, DestinationFile);

                // wait until result is completed
                while (result.IsCompleted == false)
                {
                    Thread.Sleep(1000);
                }

                if (result.Result == false)
                {
                    // populate output
                    results.Add("Success", "False");

                    Logging.Log(Logging.LogType.Critical, "RetroAchievements", $"Failed to download {DataType} metadata database from RetroAchievements");
                    return results;
                }

                // Logging.Log(Logging.LogType.Information, "RetroAchievements", $"Completed download of {DataType} metadata database from RetroAchievements");

                // populate output
                results.Add("Success", result.Result.ToString());
                results.Add("Age", "0");
                results.Add("Downloaded", "True");
            }
            else
            {
                // calculate the age of the file
                var lastWriteTime = File.GetLastWriteTime(DestinationFile);
                var age = DateTime.Now - lastWriteTime;

                // Logging.Log(Logging.LogType.Information, "RetroAchievements", $"{DataType} metadata database from RetroAchievements is up to date. Next update in " + (MaxAgeInDays - age.Days) + " days");

                // populate output
                results.Add("Success", "True");
                results.Add("Age", age.Days.ToString());
                results.Add("Downloaded", "False");
            }

            // wait an extra second before downloading the next file if we downloaded the file
            if (results["Downloaded"] == "True")
            {
                Thread.Sleep(1000);
            }

            return results;
        }

        public async Task<bool?> DownloadFile(string url, string DestinationFile)
        {
            var result = await _DownloadFile(new Uri(url), DestinationFile);

            return result;
        }

        private async Task<bool?> _DownloadFile(Uri uri, string DestinationFile)
        {
            // Logging.Log(Logging.LogType.Information, "Communications", "Downloading from " + uri.ToString() + " to " + DestinationFile);

            try
            {
                using (HttpResponseMessage response = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream contentStream = await response.Content.ReadAsStreamAsync(), fileStream = new FileStream(DestinationFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var totalRead = 0L;
                        var totalReads = 0L;
                        var buffer = new byte[8192];
                        var isMoreToRead = true;

                        do
                        {
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fileStream.WriteAsync(buffer, 0, read);

                                totalRead += read;
                                totalReads += 1;

                                if (totalReads % 2000 == 0)
                                {
                                    Console.WriteLine(string.Format("total bytes downloaded so far: {0:n0}", totalRead));
                                }
                            }
                        }
                        while (isMoreToRead);
                    }
                }

                return true;
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    if (File.Exists(DestinationFile))
                    {
                        FileInfo fi = new FileInfo(DestinationFile);
                        if (fi.Length == 0)
                        {
                            File.Delete(DestinationFile);
                        }
                    }
                }

                Logging.Log(Logging.LogType.Warning, "Communications", "Error downloading file: ", ex);
            }

            return false;
        }

        private CompileXMLDatResponse? CompileXMLDat(PlatformModel platform, string targetPath)
        {
            if (platform == null)
            {
                return null;
            }

            // setup the DAT output file
            string datFileName = Path.Combine(targetPath, "RetroAchievements - " + platform.Name.Replace("/", "-") + ".dat");
            string tmpDatFileName = Path.Combine(targetPath, "RetroAchievements - " + platform.Name.Replace("/", "-") + ".tmp");

            if (File.Exists(tmpDatFileName))
            {
                File.Delete(tmpDatFileName);
            }

            // create the DAT XML file for the platform
            using (XmlTextWriter writer = new XmlTextWriter(tmpDatFileName, null))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;

                writer.WriteStartDocument();
                writer.WriteStartElement("datafile");
                writer.WriteStartElement("header");
                writer.WriteStartElement("name");
                writer.WriteString(platform.Name);
                writer.WriteEndElement();
                writer.WriteStartElement("description");
                writer.WriteString(platform.Name);
                writer.WriteEndElement();
                writer.WriteStartElement("category");
                writer.WriteString("RetroAchievements");
                writer.WriteEndElement();
                writer.WriteStartElement("version");
                writer.WriteString(DateTime.UtcNow.ToString("yyyy-MM-dd"));
                writer.WriteEndElement();
                writer.WriteStartElement("author");
                writer.WriteString("RetroAchievements");
                writer.WriteEndElement();
                writer.WriteStartElement("date");
                writer.WriteString(DateTime.Now.ToString("yyyy-MM-dd"));
                writer.WriteEndElement();
                writer.WriteStartElement("comment");
                writer.WriteString("This file was generated by the Hasheous service.");
                writer.WriteEndElement();
                writer.WriteEndElement();

                if (platform.Games != null)
                {
                    foreach (var game in platform.Games)
                    {
                        // get the game categories from the game's title - any text between "~" and "~ " is a category and the rest is the game name. There could be multiple categories.
                        string gameName = game.Title;
                        string category = "";
                        if (gameName.Contains("~ "))
                        {
                            string pattern = @"~(.*?)~\s";
                            MatchCollection matches = Regex.Matches(gameName, pattern);
                            foreach (Match match in matches)
                            {
                                if (category.Length > 1)
                                {
                                    category += ",";
                                }
                                category += match.Groups[1].Value.Trim();
                            }

                            // set gameName to everything after the last "~ "
                            gameName = gameName.Substring(gameName.LastIndexOf("~ ") + 2);
                        }

                        writer.WriteStartElement("game");
                        writer.WriteAttributeString("name", gameName);
                        writer.WriteAttributeString("id", game.ID.ToString());
                        writer.WriteAttributeString("achievements", game.NumAchievements.ToString());
                        writer.WriteAttributeString("points", game.Points.ToString());
                        writer.WriteAttributeString("retroachievements", game.NumAchievements.ToString());
                        writer.WriteAttributeString("retroachievementslink", "https://retroachievements.org/Game/" + game.ID);
                        writer.WriteAttributeString("retroachievementsimage", game.ImageIcon);

                        writer.WriteStartElement("description");
                        writer.WriteString(gameName);
                        writer.WriteEndElement();

                        writer.WriteStartElement("category");
                        writer.WriteString(category);
                        writer.WriteEndElement();

                        if (game.GameHashes != null)
                        {
                            foreach (var hash in game.GameHashes)
                            {
                                writer.WriteStartElement("rom");
                                writer.WriteAttributeString("name", hash.Name);
                                writer.WriteAttributeString("md5", hash.MD5);
                                writer.WriteAttributeString("labels", string.Join(",", hash.Labels.ToArray()));
                                writer.WriteEndElement();
                            }
                        }

                        writer.WriteEndElement();
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            // generate an MD5 hash of the DAT file
            string md5 = "";
            using (var md5Hash = MD5.Create())
            {
                using (var stream = File.OpenRead(tmpDatFileName))
                {
                    byte[] hash = md5Hash.ComputeHash(stream);
                    md5 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }

            bool allowOverwrite = true;
            if (File.Exists(datFileName))
            {
                // generate an MD5 hash of the existing DAT file
                string existingMd5 = "";
                using (var md5Hash = MD5.Create())
                {
                    using (var stream = File.OpenRead(datFileName))
                    {
                        byte[] hash = md5Hash.ComputeHash(stream);
                        existingMd5 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }

                // if the MD5 hashes are the same, don't overwrite the file
                if (md5 == existingMd5)
                {
                    allowOverwrite = false;
                }
            }

            // if we're allowed to overwrite the file, do it
            if (allowOverwrite)
            {
                if (File.Exists(datFileName))
                {
                    File.Delete(datFileName);
                }

                File.Move(tmpDatFileName, datFileName);
            }
            else
            {
                File.Delete(tmpDatFileName);
            }

            // return the DAT file name
            return new CompileXMLDatResponse
            {
                FileName = datFileName,
                MD5 = md5,
                allowOverwrite = allowOverwrite
            };
        }

        private class CompileXMLDatResponse
        {
            public string FileName { get; set; }
            public string MD5 { get; set; }
            public bool allowOverwrite { get; set; }
        }
    }
}