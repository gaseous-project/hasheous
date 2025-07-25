using Classes;

namespace hasheous_server.Models
{
    /// <summary>
    /// Model for observations made on archived content
    /// This model is used to store details about the archive and its content
    /// </summary>
    public class ArchiveObservationModel
    {
        /// <summary>
        /// Details about the archive itself, including its hashes and size
        /// </summary>
        /// <remarks>
        /// The archive details include the MD5, SHA1, SHA256 hashes, CRC (if available), size, and type of the archive.
        /// The type is typically the file extension of the archive (e.g., .zip, .rar).
        /// </remarks>
        public ArchiveDetails Archive { get; set; } = new ArchiveDetails();

        /// <summary>
        /// Details about the ROM matched with this archive.
        /// /// This includes the hashes of the ROM and is used to link the archive to a specific ROM.
        /// </summary>
        /// <remarks>
        /// The content details must include one of the following hashes: MD5, SHA1, SHA256, or CRC.
        /// This information is crucial for identifying the ROM within the archive and ensuring its integrity.
        /// </remarks>
        public ContentDetails Content { get; set; } = new ContentDetails();

        public class ArchiveDetails
        {
            /// <summary>
            /// The MD5 hash of the archive
            /// </summary>
            /// <remarks>
            /// This is a unique identifier for the archive file, used to verify its integrity.
            /// </remarks>
            /// <example>e99a18c428cb38d5f260853678922e03</example>
            public string MD5 { get; set; }

            /// <summary>
            /// The SHA1 hash of the archive
            /// </summary>
            /// <remarks>
            /// This is another unique identifier for the archive file, providing a different level of integrity verification.
            /// </remarks>
            /// <example>5baa61e4c9b93f3f0682250b6cf8331b7ee68fd8</example>
            public string SHA1 { get; set; }

            /// <summary>
            /// The SHA256 hash of the archive
            /// </summary>
            /// <remarks>
            /// This is a more secure hash for the archive file, offering a higher level of integrity verification.
            /// </remarks>
            /// <example>6dcd4ce23d88e2ee9568ba546c007c63a0b3f5b1f7e9c8f3b2f1e4c5a6b7d8e9</example>
            public string SHA256 { get; set; }

            /// <summary>
            /// The CRC (Cyclic Redundancy Check) of the archive
            /// </summary>
            /// <remarks>
            /// This is an optional field that provides an additional integrity check for the archive file.
            /// It is not always available, but when present, it can help verify the archive matches.
            /// </remarks>
            /// <example>1a79a4d60de6718e8e5b326e338ae533</example>
            public string? CRC { get; set; }

            /// <summary>
            /// The size of the archive in bytes
            /// </summary>
            /// <remarks>
            /// This is the total size of the archive file, which can be useful for determining its content and for storage management.
            /// </remarks>
            /// <example>123456789</example>
            public long Size { get; set; }

            /// <summary>
            /// The type of the archive (e.g., .zip, .rar)
            /// </summary>
            /// <remarks>
            /// This indicates the format of the archive file, which can affect how it is processed and extracted.
            /// It is typically the file extension of the archive.
            /// </remarks>
            /// <example>.zip</example>
            /// <example>.rar</example>
            /// <example>.7z</example>
            public string Type { get; set; }
        }

        public class ContentDetails
        {
            /// <summary>
            /// The MD5 hash of the ROM content
            /// </summary>
            /// <remarks>
            /// This is a unique identifier for the ROM file, used to verify its integrity.
            /// </remarks>
            /// <example>e99a18c428cb38d5f260853678922e03</example>
            public string? MD5 { get; set; }

            /// <summary>
            /// The SHA1 hash of the ROM content
            /// </summary>
            /// <remarks>
            /// This is another unique identifier for the ROM file, providing a different level of integrity verification.
            /// </remarks>
            /// <example>5baa61e4c9b93f3f0682250b6cf8331b7ee68fd8</example>
            public string? SHA1 { get; set; }

            /// <summary>
            /// The SHA256 hash of the ROM content
            /// </summary>
            /// <remarks>
            /// This is a more secure hash for the ROM file, offering a higher level of integrity verification.
            /// </remarks>
            /// <example>6dcd4ce23d88e2ee9568ba546c007c63a0b3f5b1f7e9c8f3b2f1e4c5a6b7d8e9</example>
            public string? SHA256 { get; set; }

            /// <summary>
            /// The CRC (Cyclic Redundancy Check) of the ROM content
            /// </summary>
            public string? CRC { get; set; }
        }
    }
}