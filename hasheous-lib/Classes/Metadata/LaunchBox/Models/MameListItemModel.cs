using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox MAME list item mapping a ROM file to a named list.</summary>
    public class MameListItemModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_MameListItem_FileName", "FileName"),
                ModelIndexDefinition.Single("IX_MameListItem_ListName", "ListName"),
                ModelIndexDefinition.Composite("IX_MameListItem_ListName_FileName", false, "ListName", "FileName")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>ROM file name.</summary>
        [XmlElement("FileName")]
        public string? FileName { get; set; }

        /// <summary>Display name of the game.</summary>
        [XmlElement("GameName")]
        public string? GameName { get; set; }

        /// <summary>Name of the MAME list this item belongs to.</summary>
        [XmlElement("ListName")]
        public string? ListName { get; set; }
    }
}
