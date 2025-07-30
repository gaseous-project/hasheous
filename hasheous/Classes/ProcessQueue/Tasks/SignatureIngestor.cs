namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that ingests signature files using various parsers.
    /// </summary>
    public class SignatureIngestor : IQueueTask
    {
        /// <inheritdoc/>
        public string TaskName { get; set; } = "SignatureIngestor";

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            XML.XMLIngestor tIngest = new XML.XMLIngestor();

            foreach (int i in Enum.GetValues(typeof(gaseous_signature_parser.parser.SignatureParser)))
            {
                gaseous_signature_parser.parser.SignatureParser parserType = (gaseous_signature_parser.parser.SignatureParser)i;
                if (
                    parserType != gaseous_signature_parser.parser.SignatureParser.Auto &&
                    parserType != gaseous_signature_parser.parser.SignatureParser.Unknown
                )
                {

                    string SignaturePath = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, parserType.ToString());
                    string SignatureProcessedPath = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesProcessedDirectory, parserType.ToString());

                    if (!Directory.Exists(SignaturePath))
                    {
                        Directory.CreateDirectory(SignaturePath);
                    }

                    if (!Directory.Exists(SignatureProcessedPath))
                    {
                        Directory.CreateDirectory(SignatureProcessedPath);
                    }

                    await tIngest.Import(SignaturePath, SignatureProcessedPath, parserType);
                }
            }

            return null; // Assuming the method returns void, we return null here.
        }
    }
}