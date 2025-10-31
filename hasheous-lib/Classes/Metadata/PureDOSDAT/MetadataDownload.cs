using Classes;
using hasheous_server.Classes;

namespace PureDOSDAT
{
    public class DownloadManager
    {
        public static string GitUrl { get; } = "https://github.com/PureDOS/DAT.git";

        public static string GitBranch { get; } = "main";

        public static string SourceName { get; } = "PureDOSDAT";

        public async Task Download()
        {
            try
            {
                // setup output directory
                string extractDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_PureDOSDAT);

                // clone the repository
                try
                {
                    bool cloneSuccess = await DownloadTools.CloneOrRefreshRepoAsync(GitUrl, GitBranch, extractDir);
                    if (!cloneSuccess)
                    {
                        Logging.Log(Logging.LogType.Warning, SourceName, $"{SourceName} repository is already up to date; no changes detected.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to clone or refresh ${SourceName} repository from '{GitUrl}': {ex.Message}", ex);
                }

                // cleanup signature processed directory
                string tosecProcessedDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesProcessedDirectory, SourceName);
                if (Directory.Exists(tosecProcessedDir)) { Directory.Delete(tosecProcessedDir, true); }

                // copy the signature files to the processing directory
                string datFilePath = extractDir;
                if (Directory.Exists(datFilePath))
                {
                    string signatureDestDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, SourceName);
                    if (Directory.Exists(signatureDestDir))
                    {
                        Directory.Delete(signatureDestDir, true);
                    }
                    Directory.CreateDirectory(signatureDestDir);

                    foreach (string file in Directory.GetFiles(datFilePath, "*.xml", SearchOption.TopDirectoryOnly))
                    {
                        string destFileName = Path.GetFileNameWithoutExtension(file) + ".dat";
                        string destFile = Path.Combine(signatureDestDir, destFileName);
                        File.Copy(file, destFile);

                        Logging.Log(Logging.LogType.Information, SourceName, $"{SourceName} metadata file copied to processing directory: {destFile}");
                    }
                }
                else
                {
                    throw new Exception($"{SourceName} metadata files not found in cloned repository: {datFilePath}");
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