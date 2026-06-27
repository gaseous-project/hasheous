using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox File entry mapping a ROM/disc file to its platform and game name.</summary>
    public class FileModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_File_FileName", "FileName"),
                ModelIndexDefinition.Single("IX_File_GameId", "GameId"),
                ModelIndexDefinition.Composite("IX_File_Platform_GameId", false, "Platform", "GameId")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>Platform name the file belongs to.</summary>
        [XmlElement("Platform")]
        [ForeignKey("Platform", typeof(PlatformModel), "Name", "Id")]
        public string? Platform { get; set; }

        /// <summary>File name of the ROM/disc image.</summary>
        [XmlElement("FileName")]
        public string? FileName { get; set; }

        /// <summary>
        /// Game name from XML, resolved to the referenced Game.Id during import.
        /// This remains string-typed so FK resolution can look up by Name.
        /// </summary>
        [XmlElement("GameName")]
        [ForeignKey("Game", typeof(GameModel), "Name", "Id")]
        public string? GameId { get; set; }
    }
}
