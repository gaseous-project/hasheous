namespace DATImport
{
    public interface IDATFileImport
    {
        /// <summary>
        /// The source type of the signature parser to use for this import.
        /// </summary>
        public gaseous_signature_parser.parser.SignatureParser SourceType { get; }

        /// <summary>
        /// The interval at which the import should be performed, in minutes. This can be used to schedule periodic imports or to determine how frequently the import should be executed. Minimum value is 1440 minutes (24 hours). If the value is less than 1440, it will be set to 1440.
        /// </summary>
        public int Interval { get; }

        /// <summary>
        /// Indicates whether the import is enabled or disabled. If set to true, the import will be executed according to the specified interval. If set to false, the import will be skipped and not executed. This property can be used to control the execution of the import based on user preferences or system conditions.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// Stage the files for processing. This may involve downloading and extracting files, in addition to moving the resultant files to the appropriate processing directory. The implementation of this method should handle any necessary file operations to prepare the files for processing.
        /// </summary>
        public Task StageFiles();

        /// <summary>
        /// Perform any post processing steps after the files have been staged. This may involve parsing the files, extracting relevant data, and performing any necessary transformations or validations. The implementation of this method should handle any necessary processing steps to prepare the data for further use.
        /// </summary>
        public Task ProcessFiles();

        /// <summary>
        /// Validate the staged files to ensure they meet the expected format and content requirements. This may involve checking for the presence of required files, verifying file integrity, and validating the data against predefined schemas or rules. The implementation of this method should handle any necessary validation steps to ensure the files are suitable for further processing.
        /// </summary>
        /// <returns>A task that represents the asynchronous validation operation. The task result is a boolean indicating whether the files are valid (true) or not (false).</returns>
        public Task<bool> ValidateFiles();
    }
}