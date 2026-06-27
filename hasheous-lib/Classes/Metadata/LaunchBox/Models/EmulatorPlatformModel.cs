using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox mapping of an emulator to a platform from Metadata.xml.</summary>
    public class EmulatorPlatformModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_EmulatorPlatform_Platform", "Platform"),
                ModelIndexDefinition.Single("IX_EmulatorPlatform_Emulator", "Emulator"),
                ModelIndexDefinition.Composite("IX_EmulatorPlatform_Platform_Emulator", false, "Platform", "Emulator")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>Semicolon-separated list of file extensions for this platform/emulator combination.</summary>
        [XmlElement("ApplicableFileExtensions")]
        public string? ApplicableFileExtensions { get; set; }

        /// <summary>Platform-specific command-line override.</summary>
        [XmlElement("CommandLine")]
        public string? CommandLine { get; set; }

        /// <summary>Emulator name this mapping refers to.</summary>
        [XmlElement("Emulator")]
        public string? Emulator { get; set; }

        /// <summary>Platform name this mapping applies to.</summary>
        [XmlElement("Platform")]
        [ForeignKey("Platform", typeof(PlatformModel), "Name", "Id")]
        public string? Platform { get; set; }

        /// <summary>Whether this emulator is the recommended choice for the platform.</summary>
        [XmlElement("Recommended")]
        public bool Recommended { get; set; }

        /// <summary>Required BIOS file name, if any.</summary>
        [XmlElement("RequiredBiosFile")]
        public string? RequiredBiosFile { get; set; }
    }
}
