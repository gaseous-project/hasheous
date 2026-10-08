using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Classes;
using DATImport;
using gaseous_signature_parser;
using gaseous_signature_parser.classes;
using gaseous_signature_parser.classes.bracketparsers;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Classes;

namespace libretro
{
    public class DownloadManager : IDATFileImport
    {
        [ModuleInitializer]
        public static void RegisterImporter() => SignatureIngestor.Register<DownloadManager>();

        /// <inheritdoc/>
        public DATImport.SignatureIngestor.SignatureType SourceType => DATImport.SignatureIngestor.SignatureType.libretro;

        /// <inheritdoc/>
        public int Interval => 10080; // 7 days in minutes

        /// <inheritdoc/>
        public bool IsEnabled => true; // Always enabled for metadata download

        private static string GitUrl { get; } = "https://github.com/libretro/libretro-database.git";

        private static string GitBranch { get; } = "master";

        private static string GitDirectory { get; } = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_libretro, "libretro-database");

        private static string SourceName { get; } = "libretro";

        /// <inheritdoc/>
        public async Task StageFiles()
        {
            try
            {
                // clone the repository
                try
                {
                    bool cloneSuccess = await DownloadTools.CloneOrRefreshRepoAsync(GitUrl, GitBranch, GitDirectory);
                    if (!cloneSuccess)
                    {
                        Logging.Log(Logging.LogType.Warning, SourceName, $"{SourceName} repository is already up to date; no changes detected.");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to clone or refresh {SourceName} repository from '{GitUrl}': {ex.Message}", ex);
                }

                // copy the dat and metadat directories to the signature ingest directory - restrict copying to the specific files needed
                string[] datsToCopy = new[]
                {
                    "Amstrad - CPC",
                    "Arduboy Inc - Arduboy",
                    "Atomiswave",
                    "CHIP-8",
                    "Commodore - Amiga",
                    "Commodore - CD32",
                    "DOS",
                    "Enterprise - 128",
                    "Handheld Electronic Game",
                    "HBMAME",
                    "NEC - PC-98",
                    "Nintendo - GameCube",
                    "Nintendo - Nintendo Entertainment System",
                    "Nintendo - Super Nintendo Entertainment System",
                    "Nintendo - Wii U",
                    "Nintendo - Wii",
                    "PICO-8",
                    "ScummVM",
                    "SNK - Neo Geo",
                    "Sega - Saturn",
                    "Sinclair - ZX 81",
                    "Sinclair - ZX Spectrum",
                    "Sony - PlayStation 3",
                    "Sony - PlayStation Minis",
                    "TIC-80",
                    "Uzebox",
                    "Videotron - TV-Computer",
                    "Vircon32"
                };

                string srcDatDirectory = Path.Combine(GitDirectory, "dat");
                string srcMetadataDirectory = Path.Combine(GitDirectory, "metadat");

                string destDatDirectory = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "libretro", "dat");
                string destMetadataDirectory = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "libretro", "metadat");

                string[] metadataDirectories = Directory.GetDirectories(srcMetadataDirectory, "*", SearchOption.TopDirectoryOnly);

                // clean destination directories before copying
                if (Directory.Exists(destDatDirectory))
                {
                    Directory.Delete(destDatDirectory, true);
                }
                if (Directory.Exists(destMetadataDirectory))
                {
                    Directory.Delete(destMetadataDirectory, true);
                }

                // create destination directories if they don't exist
                if (!Directory.Exists(destDatDirectory))
                {
                    Directory.CreateDirectory(destDatDirectory);
                }
                if (!Directory.Exists(destMetadataDirectory))
                {
                    Directory.CreateDirectory(destMetadataDirectory);
                }

                // start copying files
                foreach (string fileName in datsToCopy)
                {
                    string srcFilePath = Path.Combine(srcDatDirectory, fileName + ".dat");
                    string destFilePath = Path.Combine(destDatDirectory, fileName + ".dat");
                    if (File.Exists(srcFilePath))
                    {
                        File.Copy(srcFilePath, destFilePath);

                        foreach (string metadataDirectory in metadataDirectories)
                        {
                            string srcMetadataFilePath = Path.Combine(metadataDirectory, fileName + ".dat");
                            if (File.Exists(srcMetadataFilePath))
                            {
                                string destMetadataSubDirectory = Path.Combine(destMetadataDirectory, Path.GetFileName(metadataDirectory));
                                if (!Directory.Exists(destMetadataSubDirectory))
                                {
                                    Directory.CreateDirectory(destMetadataSubDirectory);
                                }
                                string destMetadataFilePath = Path.Combine(destMetadataSubDirectory, fileName + ".dat");
                                File.Copy(srcMetadataFilePath, destMetadataFilePath);
                            }
                        }
                    }
                }

                Logging.Log(Logging.LogType.Information, SourceName, $"{SourceName} metadata processing completed successfully.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, SourceName, $"Error downloading {SourceName} metadata: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public Task ProcessFiles()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<bool> ValidateFiles()
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task PostImport()
        {
            throw new NotImplementedException();
        }
    }
}