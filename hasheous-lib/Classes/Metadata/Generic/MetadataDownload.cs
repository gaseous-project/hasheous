using System.Runtime.CompilerServices;
using DATImport;

namespace Generic
{
    public class DownloadManager : IDATFileImport
    {
        [ModuleInitializer]
        public static void RegisterImporter() => SignatureIngestor.Register<DownloadManager>();

        /// <inheritdoc/>
        public gaseous_signature_parser.parser.SignatureParser SourceType => gaseous_signature_parser.parser.SignatureParser.Generic;

        /// <inheritdoc/>
        public int Interval => 10080; // 7 days in minutes

        /// <inheritdoc/>
        public bool IsEnabled => true; // Always enabled for metadata download

        /// <inheritdoc/>
        public async Task StageFiles()
        {
            return; // No staging required for Generic metadata
        }

        /// <inheritdoc/>
        public async Task ProcessFiles()
        {
            return; // No processing required for Generic metadata
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateFiles()
        {
            return true; // Always valid for Generic metadata
        }
    }
}