namespace hasheous_server.Models
{
    /// <summary>
    /// Describes the content to search for. Provide at least one value.
    /// </summary>
    public class HashLookupModel
    {
        /// <summary>
        /// MD5 hash of the content
        /// </summary>
        public string? MD5 { get; set; }

        /// <summary>
        /// SHA1 hash of the content
        /// </summary>
        public string? SHA1 { get; set; }

        /// <summary>
        /// SHA256 hash of the content
        /// </summary>
        public string? SHA256 { get; set; }

        /// <summary>
        /// CRC32 hash of the content
        /// </summary>
        public string? CRC { get; set; }
    }
}