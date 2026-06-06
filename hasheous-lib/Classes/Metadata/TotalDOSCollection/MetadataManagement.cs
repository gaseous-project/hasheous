using System.Text;
using Classes;

namespace TotalDOSCollection
{
    public class MetadataManagement : BaseParser
    {
        private const string HeaderTag = "DOSCenter";
        private static readonly string[] EntryTags = { "game" };
        private static readonly string[] ChildTags = { "file", "rom" };

        /// <summary>
        /// Reads the DAT file and if it's larger than 40MB, it will break the file into smaller parts and save them in the signatures directory for import. This is necessary because the DAT file is too large to be imported directly into the database, and breaking it into smaller parts allows for more efficient processing and importing.
        /// </summary>
        public void VerifyDATFile()
        {
            string datFilePath = System.IO.Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, gaseous_signature_parser.parser.SignatureParser.TotalDOSCollection.ToString(), "tdc_daily.dat");

            const long fileSizeLimitInBytes = 10L * 1024 * 1024;

            if (!System.IO.File.Exists(datFilePath))
                return;

            long fileSizeInBytes = new System.IO.FileInfo(datFilePath).Length;
            if (fileSizeInBytes <= fileSizeLimitInBytes)
                return;

            // Extract the header from the DAT file — prepended to every part file.
            var header = ExtractHeaderData(datFilePath, HeaderTag);
            string headerString = HeaderTag + " (\n" +
                string.Join(Environment.NewLine, header.Select(kvp => $"\t{kvp.Key}: {kvp.Value}")) +
                "\n)";
            int headerByteCount = Encoding.UTF8.GetByteCount(headerString + Environment.NewLine);

            string outputDir = System.IO.Path.GetDirectoryName(datFilePath)!;
            int partNumber = 1;
            StreamWriter? currentWriter = null;
            long currentPartSize = 0;

            void OpenNextPart()
            {
                currentWriter?.Dispose();
                string partPath = System.IO.Path.Combine(outputDir, $"tdc_daily_{partNumber:D4}.dat");
                currentWriter = new StreamWriter(partPath, false, Encoding.UTF8);
                currentWriter.WriteLine(headerString);
                currentPartSize = headerByteCount;
                partNumber++;
            }

            try
            {
                OpenNextPart();

                foreach (var (tag, content) in StreamRawEntryBlocks(datFilePath, EntryTags))
                {
                    string entryText = $"{tag} (\n{content}\n)\n";
                    int entryByteCount = Encoding.UTF8.GetByteCount(entryText);

                    // Start a new part if this entry would push us over the limit,
                    // but always write at least one entry per part to avoid an infinite loop
                    // on entries that are individually larger than the limit.
                    if (currentPartSize + entryByteCount > fileSizeLimitInBytes && currentPartSize > headerByteCount)
                        OpenNextPart();

                    currentWriter!.Write(entryText);
                    currentPartSize += entryByteCount;
                }
            }
            finally
            {
                currentWriter?.Dispose();
            }

            // Rename the original oversized file so it is not re-ingested alongside the parts.
            System.IO.File.Move(datFilePath, datFilePath + ".bak");
        }
    }
}