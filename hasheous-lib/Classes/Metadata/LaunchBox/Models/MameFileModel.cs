using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox MAME ROM file metadata entry.</summary>
    public class MameFileModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_MameFile_FileName", "FileName"),
                ModelIndexDefinition.Single("IX_MameFile_Name", "Name"),
                ModelIndexDefinition.Single("IX_MameFile_CloneOf", "CloneOf"),
                ModelIndexDefinition.Composite("IX_MameFile_Name_Region", false, "Name", "Region")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>Parent ROM this is a clone of, if any.</summary>
        [XmlElement("CloneOf")]
        public string? CloneOf { get; set; }

        /// <summary>Developer of the arcade game.</summary>
        [XmlElement("Developer")]
        public string? Developer { get; set; }

        /// <summary>ROM file name.</summary>
        [XmlElement("FileName")]
        public string? FileName { get; set; }

        /// <summary>Game genre.</summary>
        [XmlElement("Genre")]
        public string? Genre { get; set; }

        [XmlElement("IsBootleg")]
        public bool IsBootleg { get; set; }

        [XmlElement("IsCasino")]
        public bool IsCasino { get; set; }

        [XmlElement("IsFruit")]
        public bool IsFruit { get; set; }

        [XmlElement("IsHack")]
        public bool IsHack { get; set; }

        [XmlElement("IsMahjong")]
        public bool IsMahjong { get; set; }

        [XmlElement("IsMature")]
        public bool IsMature { get; set; }

        [XmlElement("IsMechanical")]
        public bool IsMechanical { get; set; }

        [XmlElement("IsNonArcade")]
        public bool IsNonArcade { get; set; }

        [XmlElement("IsPlayChoice")]
        public bool IsPlayChoice { get; set; }

        [XmlElement("IsPrototype")]
        public bool IsPrototype { get; set; }

        [XmlElement("IsQuiz")]
        public bool IsQuiz { get; set; }

        [XmlElement("IsRhythm")]
        public bool IsRhythm { get; set; }

        [XmlElement("IsTableTop")]
        public bool IsTableTop { get; set; }

        /// <summary>Language(s) supported.</summary>
        [XmlElement("Language")]
        public string? Language { get; set; }

        /// <summary>Display name of the game.</summary>
        [XmlElement("Name")]
        public string? Name { get; set; }

        /// <summary>Play mode (e.g. Single Player, Multiplayer).</summary>
        [XmlElement("PlayMode")]
        public string? PlayMode { get; set; }

        /// <summary>Publisher of the arcade game.</summary>
        [XmlElement("Publisher")]
        public string? Publisher { get; set; }

        /// <summary>Region of release.</summary>
        [XmlElement("Region")]
        public string? Region { get; set; }

        /// <summary>Series the game belongs to.</summary>
        [XmlElement("Series")]
        public string? Series { get; set; }

        /// <summary>Source driver name in MAME.</summary>
        [XmlElement("Source")]
        public string? Source { get; set; }

        /// <summary>Emulation status (e.g. Good, Imperfect).</summary>
        [XmlElement("Status")]
        public string? Status { get; set; }

        /// <summary>Version string.</summary>
        [XmlElement("Version")]
        public string? Version { get; set; }

        /// <summary>Release year (stored as string to accommodate ranges or missing values).</summary>
        [XmlElement("Year")]
        public string? Year { get; set; }
    }
}
