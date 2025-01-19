using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using Classes;

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

        public string Download()
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
                var result = DownloadFile(Url, downloadZipFile);

                // wait until result is completed
                while (result.IsCompleted == false)
                {
                    Thread.Sleep(1000);
                }

                if (result.Result == false)
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
                db.ExecuteCMD(sql);

                // create the new database
                sql = "CREATE DATABASE `thegamesdb`;";
                db.ExecuteCMD(sql);

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
            var result = await _DownloadFile(new Uri(url), DestinationFile);

            return result;
        }

        private async Task<bool?> _DownloadFile(Uri uri, string DestinationFile)
        {
            Logging.Log(Logging.LogType.Information, "Communications", "Downloading from " + uri.ToString() + " to " + DestinationFile);

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

                Logging.Log(Logging.LogType.Warning, "Download Images", "Error downloading file: ", ex);
            }

            return false;
        }
    }
}