using System.Runtime.CompilerServices;
using Classes;
using DATImport;
using hasheous_server.Classes;

namespace PureDOSDAT
{
    public class DownloadManager : IDATFileImport
    {
        [ModuleInitializer]
        public static void RegisterImporter() => SignatureIngestor.Register<DownloadManager>();

        /// <inheritdoc/>
        public gaseous_signature_parser.parser.SignatureParser SourceType => gaseous_signature_parser.parser.SignatureParser.PureDOSDAT;

        /// <inheritdoc/>
        public int Interval => 10080; // 7 days in minutes

        /// <inheritdoc/>
        public bool IsEnabled => true; // Always enabled for metadata download

        private static string GitUrl { get; } = "https://github.com/PureDOS/DAT.git";

        private static string GitBranch { get; } = "main";

        private static string SourceName { get; } = "PureDOSDAT";

        /// <inheritdoc/>
        public async Task StageFiles()
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
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to clone or refresh {SourceName} repository from '{GitUrl}': {ex.Message}", ex);
                }

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

        /// <inheritdoc/>
        public async Task ProcessFiles()
        {
            return; // No additional processing needed for PureDOSDAT metadata
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateFiles()
        {
            // Implement validation logic if needed
            return true; // No validation needed for PureDOSDAT metadata
        }
    }
}