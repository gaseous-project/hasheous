using Classes;
using hasheous_server.Classes;

namespace FBNEO
{
    public class DownloadManager
    {
        public static string GitUrl { get; } = "https://github.com/libretro/FBNeo.git";

        public static string GitBranch { get; } = "master";

        public static string SourceName { get; } = "FBNEO";

        public async Task Download()
        {
            try
            {
                // setup output directory
                string extractDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_FBNEO);

                // clone the repository
                try
                {
                    bool cloneSuccess = await DownloadTools.CloneOrRefreshRepoAsync(GitUrl, "master", extractDir);
                    if (!cloneSuccess)
                    {
                        Logging.Log(Logging.LogType.Warning, SourceName, $"{SourceName} repository is already up to date; no changes detected.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to clone or refresh {SourceName} repository from '{GitUrl}': {ex.Message}", ex);
                }

                // cleanup signature processed directory
                string tosecProcessedDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesProcessedDirectory, "FBNeo");
                if (Directory.Exists(tosecProcessedDir)) { Directory.Delete(tosecProcessedDir, true); }

                // copy the signature files to the processing directory
                string datFile = Path.Combine(extractDir, "dats");
                if (Directory.Exists(datFile))
                {
                    foreach (var file in Directory.GetFiles(datFile, "*.dat", SearchOption.TopDirectoryOnly))
                    {
                        string destFile = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "FBNeo", Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                }
                else
                {
                    throw new Exception($"DAT files directory not found in the cloned repository: {datFile}");
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