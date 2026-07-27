using System.Runtime.CompilerServices;
using Classes;
using DATImport;
using hasheous_server.Classes;

namespace WHDLoad
{
    public class DownloadManager : IDATFileImport
    {
        [ModuleInitializer]
        public static void RegisterImporter() => SignatureIngestor.Register<DownloadManager>();

        /// <inheritdoc/>
        public gaseous_signature_parser.parser.SignatureParser SourceType => gaseous_signature_parser.parser.SignatureParser.WHDLoad;

        /// <inheritdoc/>
        public int Interval => 10080; // 7 days in minutes

        /// <inheritdoc/>
        public bool IsEnabled => true; // Always enabled for metadata download

        private static string GitUrl { get; } = "https://github.com/BlitterStudio/amiberry.git";

        private static string GitBranch { get; } = "master";

        private static string SourceName { get; } = "WHDLoad";

        public async Task StageFiles()
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

        /// <inheritdoc/>
        public async Task ProcessFiles()
        {
            return; // No additional processing needed for WHDLoad metadata
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateFiles()
        {
            // Implement validation logic if needed
            return true; // No validation needed for WHDLoad metadata
        }
    }
}