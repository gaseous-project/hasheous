using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    public class PlatformModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_Platform_Name", "Name"),
                ModelIndexDefinition.Single("IX_Platform_Category", "Category"),
                ModelIndexDefinition.Composite("IX_Platform_Category_Name", false, "Category", "Name")
            };
        }

        [XmlElement("Id")]
        public long Id { get; set; }

        [XmlElement("Name")]
        public string? Name { get; set; }

        [XmlElement("Emulated")]
        public bool Emulated { get; set; }

        [XmlElement("ReleaseDate")]
        public DateTime ReleaseDate { get; set; }

        [XmlElement("Developer")]
        [ForeignKey("Company")]
        public string? Developer { get; set; }

        [XmlElement("Manufacturer")]
        [ForeignKey("Company")]
        public string? Manufacturer { get; set; }

        [XmlElement("Cpu")]
        public string? Cpu { get; set; }

        [XmlElement("Memory")]
        public string? Memory { get; set; }

        [XmlElement("Graphics")]
        public string? Graphics { get; set; }

        [XmlElement("Sound")]
        public string? Sound { get; set; }

        [XmlElement("Display")]
        public string? Display { get; set; }

        [XmlElement("Media")]
        public string? Media { get; set; }

        [XmlElement("MaxControllers")]
        public string? MaxControllers { get; set; }

        [XmlElement("Notes")]
        public string? Notes { get; set; }

        [XmlElement("Category")]
        [ForeignKey("PlatformCategory")]
        public string? Category { get; set; }

        [XmlElement("UseMameFiles")]
        public bool UseMameFiles { get; set; }
    }
}