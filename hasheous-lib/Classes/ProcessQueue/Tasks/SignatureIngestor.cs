namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that ingests signature files using various parsers.
    /// </summary>
    public class SignatureIngestor : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>{
            QueueItemType.FetchFBNEOMetadata,
            QueueItemType.FetchMAMERedumpMetadata,
            QueueItemType.FetchPureDOSDATMetadata,
            QueueItemType.FetchRedumpMetadata,
            QueueItemType.FetchRetroAchievementsMetadata,
            QueueItemType.FetchTOSECMetadata,
            QueueItemType.FetchWHDLoadMetadata
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync()
        {
            XML.XMLIngestor tIngest = new XML.XMLIngestor();

            var parserTypes = Enum.GetValues(typeof(gaseous_signature_parser.parser.SignatureParser));

            foreach (int i in parserTypes)
            {
                gaseous_signature_parser.parser.SignatureParser parserType = (gaseous_signature_parser.parser.SignatureParser)i;
                if (
                    parserType != gaseous_signature_parser.parser.SignatureParser.Auto &&
                    parserType != gaseous_signature_parser.parser.SignatureParser.Unknown
                )
                {

                    string SignaturePath = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, parserType.ToString());

                    if (!Directory.Exists(SignaturePath))
                    {
                        Directory.CreateDirectory(SignaturePath);
                    }

                    await tIngest.Import(SignaturePath, parserType);
                }
            }

            return null; // Assuming the method returns void, we return null here.
        }
    }
}