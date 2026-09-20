using System.IO.Compression;
using System.Runtime.CompilerServices;
using Classes;
using DATImport;
using hasheous_server.Classes;

namespace HackHash
{
    public class DownloadManager : IDATFileImport
    {
        [ModuleInitializer]
        public static void RegisterImporter() => SignatureIngestor.Register<DownloadManager>();

        /// <inheritdoc/>
        public gaseous_signature_parser.parser.SignatureParser SourceType => gaseous_signature_parser.parser.SignatureParser.HackHash;

        /// <inheritdoc/>
        public int Interval => 10080; // 7 days in minutes

        /// <inheritdoc/>
        public bool IsEnabled => true; // Always enabled for metadata download

        private static string SourceUrl { get; } = "https://hackhash.darkblood.uk/api/entries/export/zip?format=xml";

        private static string SourceFileName { get; } = "hackhash.zip";

        /// <inheritdoc/>
        public async Task StageFiles()
        {
            // setup temp download directories
            string tempDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryTempDirectory, "HackHash");
            if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }
            Directory.CreateDirectory(tempDir);

            // setup output directory
            string extractDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_HackHash);
            if (Directory.Exists(extractDir)) { Directory.Delete(extractDir, true); }
            Directory.CreateDirectory(extractDir);

            // Download the datfile
            Logging.Log(Logging.LogType.Information, "HackHash", $"Downloading datfile");
            Logging.SendReport(Config.LogName, null, null, $"Downloading datfile");
            string downloadPath = System.IO.Path.Combine(tempDir, SourceFileName);
            await DownloadTools.DownloadFile(new Uri(SourceUrl), downloadPath);
            // Extract the datfile
            Logging.Log(Logging.LogType.Information, "HackHash", $"Extracting datfile to {extractDir}");
            // get the name of the first entry in the zip file
            if (File.Exists(downloadPath) == false)
            {
                Logging.Log(Logging.LogType.Warning, "HackHash", $"Datfile zip not found at {tempDir}, skipping extraction.");
                return;
            }
            using (var archive = System.IO.Compression.ZipFile.OpenRead(downloadPath))
            {
                foreach (var entry in archive.Entries)
                {
                    await entry.ExtractToFileAsync(System.IO.Path.Combine(extractDir, entry.Name));
                }
            }

            // move extracted files to processing directory
            string hackhashProcessingDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "HackHash");
            if (Directory.Exists(hackhashProcessingDir)) { Directory.Delete(hackhashProcessingDir, true); }
            Directory.CreateDirectory(hackhashProcessingDir);
            foreach (var file in Directory.GetFiles(extractDir, "*.dat", SearchOption.TopDirectoryOnly))
            {
                var destFile = Path.Combine(hackhashProcessingDir, Path.GetFileName(file));
                File.Move(file, destFile);
            }

            // cleanup temp directory
            if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); }

            Logging.Log(Logging.LogType.Information, "HackHash", "HackHash metadata download and extraction completed.");

            return;
        }

        /// <inheritdoc/>
        public async Task ProcessFiles()
        {
            return; // No processing required for HackHash metadata
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateFiles()
        {
            return true; // Always valid for HackHash metadata
        }
    }
}