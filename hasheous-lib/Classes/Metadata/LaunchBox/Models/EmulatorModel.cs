using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox emulator definition from Metadata.xml.</summary>
    public class EmulatorModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_Emulator_Name", "Name"),
                ModelIndexDefinition.Single("IX_Emulator_BinaryFileName", "BinaryFileName")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>Semicolon-separated list of file extensions this emulator handles.</summary>
        [XmlElement("ApplicableFileExtensions")]
        public string? ApplicableFileExtensions { get; set; }

        /// <summary>Whether the emulator auto-extracts archives before launching.</summary>
        [XmlElement("AutoExtract")]
        public bool AutoExtract { get; set; }

        /// <summary>Emulator executable file name.</summary>
        [XmlElement("BinaryFileName")]
        public string? BinaryFileName { get; set; }

        /// <summary>Default command-line arguments.</summary>
        [XmlElement("CommandLine")]
        public string? CommandLine { get; set; }

        /// <summary>Whether to pass only the file name (not full path) to the emulator.</summary>
        [XmlElement("FileNameOnly")]
        public bool FileNameOnly { get; set; }

        /// <summary>Whether to hide the emulator console window.</summary>
        [XmlElement("HideConsole")]
        public bool HideConsole { get; set; }

        /// <summary>Display name of the emulator.</summary>
        [XmlElement("Name")]
        public string? Name { get; set; }

        /// <summary>Whether to omit quotes around the ROM path argument.</summary>
        [XmlElement("NoQuotes")]
        public bool NoQuotes { get; set; }

        /// <summary>Whether to omit the space between the command-line flag and ROM path.</summary>
        [XmlElement("NoSpace")]
        public bool NoSpace { get; set; }

        /// <summary>Homepage or download URL for the emulator.</summary>
        [XmlElement("URL")]
        public string? URL { get; set; }
    }
}
