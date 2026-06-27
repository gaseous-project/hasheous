using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox game image metadata entry from Metadata.xml.</summary>
    public class GameImageModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_GameImage_DatabaseID", "DatabaseID"),
                ModelIndexDefinition.Single("IX_GameImage_Type", "Type"),
                ModelIndexDefinition.Composite("IX_GameImage_DatabaseID_Region_Type", false, "DatabaseID", "Region", "Type")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>CRC32 hash of the image file.</summary>
        [XmlElement("CRC32")]
        public string? CRC32 { get; set; }

        /// <summary>LaunchBox database ID of the game this image belongs to.</summary>
        [XmlElement("DatabaseID")]
        public long? DatabaseID { get; set; }

        /// <summary>Image file name.</summary>
        [XmlElement("FileName")]
        public string? FileName { get; set; }

        /// <summary>Region this image applies to.</summary>
        [XmlElement("Region")]
        [ForeignKey("Region")]
        public string? Region { get; set; }

        /// <summary>Image type (e.g. Box - Front, Screenshot - Gameplay).</summary>
        [XmlElement("Type")]
        [ForeignKey("ImageType")]
        public string? Type { get; set; }
    }
}
