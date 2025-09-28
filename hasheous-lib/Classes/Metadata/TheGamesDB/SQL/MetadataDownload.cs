using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using Classes;
using hasheous_server.Classes;

namespace TheGamesDB.SQL
{
    public class DownloadManager
    {
        private static readonly HttpClient client = new HttpClient();

        public string Url
        {
            get
            {
                return "http://cdn.thegamesdb.net/tgdb_dump.zip";
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
                return Path.Combine(LocalFilePath, "database-latest.sql");
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

        public async Task<string?> Download()
        {
            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            if (IsLocalCopyOlderThanMaxAge() == true)
            {
                Logging.Log(Logging.LogType.Information, "TheGamesDb", "Downloading metadata database from TheGamesDb");

                // set up temporary working location
                string downloadZipFileToPath = Path.GetTempFileName();
                if (Directory.Exists(downloadZipFileToPath))
                {
                    Directory.Delete(downloadZipFileToPath, true);
                }
                if (File.Exists(downloadZipFileToPath))
                {
                    File.Delete(downloadZipFileToPath);
                }
                Directory.CreateDirectory(downloadZipFileToPath);

                // download the zip file
                string downloadZipFile = Path.Combine(downloadZipFileToPath, "tgdb_dump.zip");
                var result = await DownloadFile(Url, downloadZipFile);

                // wait until result is completed
                if (result == null || result == false)
                {
                    Logging.Log(Logging.LogType.Critical, "TheGamesDb", "Failed to download meadata database from TheGamesDb");
                    return null;
                }

                // extract the zip file
                string extractedFolder = Path.Combine(downloadZipFileToPath, "tgdb_dump");
                ZipFile.ExtractToDirectory(downloadZipFile, extractedFolder);

                // find the sql file
                string sqlFile = Directory.GetFiles(extractedFolder, "*.sql", SearchOption.AllDirectories).FirstOrDefault();

                // move the sql file to the correct location
                if (File.Exists(LocalFileName))
                {
                    File.Delete(LocalFileName);
                }
                File.Move(sqlFile, LocalFileName);

                // reset the last modified date
                File.SetLastWriteTime(LocalFileName, DateTime.Now);

                // clean up
                Directory.Delete(downloadZipFileToPath, true);

                Logging.Log(Logging.LogType.Information, "TheGamesDb", "Downloaded metadata database from TheGamesDb");

                // execute the sql file against the current database server
                Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionStringNoDatabase);

                // delete the existing database
                string sql = "DROP DATABASE IF EXISTS `thegamesdb`;";
                await db.ExecuteCMDAsync(sql);

                // create the new database
                sql = "CREATE DATABASE `thegamesdb`;";
                await db.ExecuteCMDAsync(sql);

                // execute mariadb command to import the sql file
                string command = "mariadb --force -h " + Config.DatabaseConfiguration.HostName + " -P " + Config.DatabaseConfiguration.Port + " -u " + Config.DatabaseConfiguration.UserName + " -p" + Config.DatabaseConfiguration.Password + " thegamesdb < " + LocalFileName;
                ProcessStartInfo psi = new ProcessStartInfo("bash", "-c \"" + command + "\"");
                psi.WorkingDirectory = Path.GetDirectoryName(LocalFileName);
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = false;
                Process process = Process.Start(psi);
                process.WaitForExit();

                // wait for the process to finish
                while (process.HasExited == false)
                {
                    Thread.Sleep(1000);
                }

                Logging.Log(Logging.LogType.Information, "TheGamesDb", "Imported metadata database from TheGamesDb");
            }
            else
            {
                // calculate the age of the file
                var lastWriteTime = File.GetLastWriteTime(LocalFileName);
                var age = DateTime.Now - lastWriteTime;

                Logging.Log(Logging.LogType.Information, "TheGamesDb", "Metadata database from TheGamesDb is up to date. Next update in " + (MaxAgeInDays - age.Days) + " days");
            }

            return LocalFileName;
        }

        public async Task<bool?> DownloadFile(string url, string DestinationFile)
        {
            var result = await DownloadTools.DownloadFile(new Uri(url), DestinationFile);

            return result;
        }
    }
}