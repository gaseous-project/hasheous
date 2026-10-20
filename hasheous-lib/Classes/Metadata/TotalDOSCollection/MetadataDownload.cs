using System.Runtime.CompilerServices;
using Classes;
using DATImport;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Classes;
using hasheous_server.Models;

namespace TotalDOSCollection
{
    public class DownloadManager : IDATFileImport
    {
        [ModuleInitializer]
        public static void RegisterImporter() => SignatureIngestor.Register<DownloadManager>();

        /// <inheritdoc/>
        public DATImport.SignatureIngestor.SignatureType SourceType => DATImport.SignatureIngestor.SignatureType.TotalDOSCollection;

        /// <inheritdoc/>
        public int Interval => 10080; // 7 days in minutes

        /// <inheritdoc/>
        public bool IsEnabled => true; // Always enabled for metadata download

        private static string DatUrl { get; } = "http://www.totaldoscollection.org/nugnugnug/tdc_daily.zip";

        /// <inheritdoc/>
        public async Task StageFiles()
        {
            string datDirectoryPath = System.IO.Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, DATImport.SignatureIngestor.SignatureType.TotalDOSCollection.ToString());
            string zipFilePath = System.IO.Path.Combine(datDirectoryPath, "tdc_daily.zip");
            string datFilePath = System.IO.Path.Combine(datDirectoryPath, "tdc_daily.dat");

            if (Directory.Exists(datDirectoryPath))
            {
                Directory.Delete(datDirectoryPath, true);
            }
            Directory.CreateDirectory(datDirectoryPath);

            // download the DAT file
            try
            {
                await DownloadTools.DownloadFile(new Uri(DatUrl), zipFilePath);
            }
            catch (Exception ex)
            {
                // handle download error
                Console.WriteLine($"Error downloading DAT file: {ex.Message}");
                throw;
            }

            // extract the DAT file
            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, datDirectoryPath);
            }
            catch (Exception ex)
            {
                // handle extraction error
                Console.WriteLine($"Error extracting DAT file: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task ProcessFiles()
        {
            // no additional processing needed for TotalDOSCollection DAT files
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateFiles()
        {
            // verify the DAT file and split it if necessary
            MetadataManagement metadataManagement = new MetadataManagement();
            metadataManagement.VerifyDATFile();

            return true;
        }

        /// <inheritdoc/>
        public Task PostImport(HasheousRomSignatureObject ImportedObject)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<DataObjectItem> ApplyDATMetadata(DataObjectItem TargetObject)
        {
            return Task.FromResult(TargetObject);
        }
    }
}