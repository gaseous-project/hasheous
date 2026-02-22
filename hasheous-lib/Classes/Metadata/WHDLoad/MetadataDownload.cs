using Classes;
using hasheous_server.Classes;

namespace WHDLoad
{
    public class DownloadManager
    {
        public static string GitUrl { get; } = "https://github.com/BlitterStudio/amiberry.git";

        public static string GitBranch { get; } = "master";

        public static string SourceName { get; } = "WHDLoad";

        public async Task Download()
        {
            try
            {
                // setup output directory
                string extractDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_WHDLoad);

                // clone the repository
                try
                {
                    bool cloneSuccess = await DownloadTools.CloneOrRefreshRepoAsync(GitUrl, GitBranch, extractDir);
                    if (!cloneSuccess)
                    {
                        Logging.Log(Logging.LogType.Warning, SourceName, $"{SourceName} repository is already up to date; no changes detected.");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to clone or refresh {SourceName} repository from '{GitUrl}': {ex.Message}", ex);
                }

                // copy the signature files to the processing directory
                string datFile = Path.Combine(extractDir, "whdboot", "game-data", "whdload_db.xml");
                if (File.Exists(datFile))
                {
                    string destDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "WHDLoad");
                    if (Directory.Exists(destDir)) { Directory.Delete(destDir, true); }
                    Directory.CreateDirectory(destDir);
                    string destFile = Path.Combine(destDir, "whdload_db.dat");
                    File.Copy(datFile, destFile);

                    Logging.Log(Logging.LogType.Information, SourceName, $"{SourceName} metadata file copied to processing directory: {destFile}");
                }
                else
                {
                    throw new Exception($"{SourceName} metadata file not found in cloned repository: {datFile}");
                }

                Logging.Log(Logging.LogType.Information, SourceName, $"{SourceName} metadata processing completed successfully.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, SourceName, $"Error downloading {SourceName} metadata: {ex.Message}");
            }
        }
    }
}