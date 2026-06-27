using System.IO.Compression;
using Classes;
using hasheous_server.Classes;
using LaunchBox.Models;
using static LaunchBox.XmlImportDescriptor;

namespace LaunchBox
{
    public class DownloadManager
    {
        private static readonly HttpClient client = new HttpClient();

        public string DumpsUrl
        {
            get
            {
                return "http://gamesdb.launchbox-app.com/Metadata.zip";
            }
        }

        public string LocalFilePath
        {
            get
            {
                return Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_LaunchBox, "Dumps");
            }
        }

        public string DownloadedZipFilePath
        {
            get
            {
                return Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_LaunchBox, "Metadata.zip");
            }
        }

        public int MaxAgeInDays { get; set; } = 30;

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

        public async Task<string?> Download()
        {
            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            if (IsLocalCopyOlderThanMaxAge(DownloadedZipFilePath) == true)
            {
                Logging.Log(Logging.LogType.Information, "LaunchBox", "Downloading metadata database from LaunchBox");

                // download the file
                if (File.Exists(DownloadedZipFilePath))
                {
                    File.Delete(DownloadedZipFilePath);
                }

                var result = await DownloadFile(DumpsUrl, DownloadedZipFilePath);

                // extract the file
                if (result == true)
                {
                    if (Directory.Exists(LocalFilePath))
                    {
                        Directory.Delete(LocalFilePath, true);
                    }
                    ZipFile.ExtractToDirectory(DownloadedZipFilePath, LocalFilePath);

                    // start the import process
                    // drop the launchbox database if it exists
                    Database db = Config.database;
                    string dropDatabaseQuery = "DROP DATABASE IF EXISTS launchbox;";
                    await db.ExecuteCMDAsync(dropDatabaseQuery);
                    // create the launchbox database
                    string createDatabaseQuery = "CREATE DATABASE launchbox;";
                    await db.ExecuteCMDAsync(createDatabaseQuery);

                    // Platforms.xml — Platform + PlatformAlternateName
                    await ImportXmlFileAsync(db, "Platforms.xml",
                        For<PlatformModel>("Platform", "Platform"),
                        For<PlatformAlternateNameModel>("PlatformAlternateName", "PlatformAlternateName"));

                    // Metadata.xml — streamed in a single pass (large file)
                    await ImportXmlFileAsync(db, "Metadata.xml",
                        For<EmulatorModel>("Emulator", "Emulator"),
                        For<EmulatorPlatformModel>("EmulatorPlatform", "EmulatorPlatform"),
                        For<GameModel>("Game", "Game"),
                        For<GameAlternateNameModel>("GameAlternateName", "GameAlternateName"),
                        For<GameImageModel>("GameImage", "GameImage"));

                    // // Files.xml — File
                    // await ImportXmlFileAsync(db, "Files.xml",
                    //     For<FileModel>("File", "File"));

                    // // Mame.xml — ControllerSupport + MameFile + MameListItem
                    // await ImportXmlFileAsync(db, "Mame.xml",
                    //     For<ControllerSupportModel>("ControllerSupport", "ControllerSupport"),
                    //     For<MameFileModel>("MameFile", "MameFile"),
                    //     For<MameListItemModel>("MameListItem", "MameListItem"));
                }
            }
            else
            {
                // calculate the age of the file
                var lastWriteTime = File.GetLastWriteTime(DownloadedZipFilePath);
                var age = DateTime.Now - lastWriteTime;

                Logging.Log(Logging.LogType.Information, "LaunchBox", "Metadata database from LaunchBox is up to date. Next update in " + (MaxAgeInDays - age.Days) + " days");
            }

            return LocalFilePath;
        }

        public async Task<bool?> DownloadFile(string url, string DestinationFile)
        {
            var result = await DownloadTools.DownloadFile(new Uri(url), DestinationFile);

            return result;
        }

        private async Task ImportXmlFileAsync(Database db, string fileName, params XmlImportDescriptor[] descriptors)
        {
            string? filePath = Directory
                .GetFiles(LocalFilePath, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Logging.Log(Logging.LogType.Warning, "LaunchBox", $"{fileName} was not found in extracted metadata package.");
                return;
            }

            Logging.Log(Logging.LogType.Information, "LaunchBox", $"Importing {fileName}...");

            Dictionary<string, int> counts = await XmlModelImporter.ImportXmlMultipleModelsAsync(
                db, "launchbox", filePath, descriptors);

            foreach (KeyValuePair<string, int> entry in counts)
                Logging.Log(Logging.LogType.Information, "LaunchBox",
                    $"Imported {entry.Value} {entry.Key} rows into launchbox.{entry.Key}.");
        }

    }
}