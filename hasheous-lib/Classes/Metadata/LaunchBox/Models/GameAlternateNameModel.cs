using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox alternate name for a game entry from Metadata.xml.</summary>
    public class GameAlternateNameModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_GameAlternateName_DatabaseID", "DatabaseID"),
                ModelIndexDefinition.Single("IX_GameAlternateName_Region", "Region"),
                ModelIndexDefinition.Composite("IX_GameAlternateName_DatabaseID_Region", false, "DatabaseID", "Region")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>The alternate name string.</summary>
        [XmlElement("AlternateName")]
        public string? AlternateName { get; set; }

        /// <summary>LaunchBox database ID of the game this alternate name belongs to.</summary>
        [XmlElement("DatabaseID")]
        public long? DatabaseID { get; set; }

        /// <summary>Region this alternate name applies to.</summary>
        [XmlElement("Region")]
        [ForeignKey("Region")]
        public string? Region { get; set; }
    }
}
