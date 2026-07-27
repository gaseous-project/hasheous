namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a queue task that ingests signature files using various parsers.
    /// </summary>
    public class SignatureIngestor : IQueueTask
    {
        /// <inheritdoc/>
        public List<QueueItemType> Blocks => new List<QueueItemType>{
            QueueItemType.SignatureIngestor
        };

        /// <inheritdoc/>
        public async Task<object?> ExecuteAsync(object? options = null)
        {
            // abort if no options are provided or if the options are not of the expected type
            if (options is null)
            {
                return null; // No options provided, nothing to process
            }

            if (options is not DATImport.IDATFileImport importer)
            {
                // this shouldn't happen, but just in case, we throw an exception to indicate that the provided options are invalid
                throw new ArgumentException("Invalid options provided. Expected a DATImport.IDATFileImport.", nameof(options));
            }

            // check the importer type
            if (importer.SourceType == gaseous_signature_parser.parser.SignatureParser.Unknown || importer.SourceType == gaseous_signature_parser.parser.SignatureParser.Auto)
            {
                return null; // Invalid importer type, nothing to process
            }

            // remove existing signature files for the importer type to avoid conflicts or duplicates
            string signaturePath = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, importer.SourceType.ToString());
            if (Directory.Exists(signaturePath))
            {
                Directory.Delete(signaturePath, true);
            }
            Directory.CreateDirectory(signaturePath);

            // start the ingestion process for the provided importer
            try
            {
                await importer.StageFiles();
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, "SignatureIngestor", $"Error during staging files for importer {importer.SourceType}: {ex.Message}", ex);
                return null; // Error occurred during staging, aborting ingestion
            }

            // perform any post-processing steps if needed
            try
            {
                await importer.ProcessFiles();
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, "SignatureIngestor", $"Error during processing files for importer {importer.SourceType}: {ex.Message}", ex);
                return null; // Error occurred during processing, aborting ingestion
            }

            // perform validation of the staged files if the importer supports it
            try
            {
                bool validFiles = await importer.ValidateFiles();
                if (!validFiles)
                {
                    Logging.Log(Logging.LogType.Warning, "SignatureIngestor", $"Validation failed for staged files of importer {importer.SourceType}. Aborting ingestion.");
                    return null; // Validation failed, aborting ingestion
                }
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, "SignatureIngestor", $"Error during validation of files for importer {importer.SourceType}: {ex.Message}", ex);
                return null; // Error occurred during validation, aborting ingestion
            }

            // files should now be in the appropriate signatures directory for importing into the database
            XML.XMLIngestor tIngest = new XML.XMLIngestor();

            string SignaturePath = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, importer.SourceType.ToString());

            // ensure the signature path exists - this should be done in the StageFiles method of the importer, but we check here as manually provided DAT files may not have been staged properly (or at all)
            if (!Directory.Exists(SignaturePath))
            {
                Directory.CreateDirectory(SignaturePath);
            }

            // start the import
            await tIngest.Import(SignaturePath, importer.SourceType);

            // clean up by moving the processed files to a "processed" directory
            string ProcessedSignatureParentPath = Config.LibraryConfiguration.LibrarySignaturesDirectory + " Processed";
            if (!Directory.Exists(ProcessedSignatureParentPath))
            {
                Directory.CreateDirectory(ProcessedSignatureParentPath);
            }
            string ProcessedSignaturePath = Path.Combine(ProcessedSignatureParentPath, importer.SourceType.ToString());

            if (Directory.Exists(ProcessedSignaturePath))
            {
                Directory.Delete(ProcessedSignaturePath, true);
            }
            Directory.Move(SignaturePath, ProcessedSignaturePath);

            // var parserTypes = Enum.GetValues(typeof(gaseous_signature_parser.parser.SignatureParser));

            // foreach (int i in parserTypes)
            // {
            //     gaseous_signature_parser.parser.SignatureParser parserType = (gaseous_signature_parser.parser.SignatureParser)i;
            //     if (
            //         parserType != gaseous_signature_parser.parser.SignatureParser.Auto &&
            //         parserType != gaseous_signature_parser.parser.SignatureParser.Unknown
            //     )
            //     {

            //         string SignaturePath = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, parserType.ToString());

            //         if (!Directory.Exists(SignaturePath))
            //         {
            //             Directory.CreateDirectory(SignaturePath);
            //         }

            //         if (parserType == gaseous_signature_parser.parser.SignatureParser.TotalDOSCollection)
            //         {
            //             TotalDOSCollection.MetadataManagement metadataManagement = new TotalDOSCollection.MetadataManagement();
            //             metadataManagement.VerifyDATFile();
            //         }

            //         await tIngest.Import(SignaturePath, parserType);
            //     }
            // }

            return null; // Assuming the method returns void, we return null here.
        }
    }
}