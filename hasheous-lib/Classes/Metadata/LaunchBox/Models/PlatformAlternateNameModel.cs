using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox alternate name entry for a platform.</summary>
    public class PlatformAlternateNameModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_PlatformAlternateName_Name", "Name"),
                ModelIndexDefinition.Single("IX_PlatformAlternateName_Alternate", "Alternate"),
                ModelIndexDefinition.Composite("IX_PlatformAlternateName_Name_Alternate", false, "Name", "Alternate")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>Alternate name string.</summary>
        [XmlElement("Alternate")]
        public string? Alternate { get; set; }

        /// <summary>Canonical platform name this alternate belongs to.</summary>
        [XmlElement("Name")]
        [ForeignKey("Platform", typeof(PlatformModel), "Name", "Id")]
        public string? Name { get; set; }
    }
}
