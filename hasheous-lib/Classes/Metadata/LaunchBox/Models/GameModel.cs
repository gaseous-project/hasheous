using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox game entry from Metadata.xml.</summary>
    public class GameModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_Game_Name", "Name"),
                ModelIndexDefinition.Single("IX_Game_DatabaseID", "DatabaseID"),
                ModelIndexDefinition.Composite("IX_Game_Platform_Name", false, "Platform", "Name")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>Average community rating (0–5 scale).</summary>
        [XmlElement("CommunityRating")]
        public double? CommunityRating { get; set; }

        /// <summary>Number of community ratings submitted.</summary>
        [XmlElement("CommunityRatingCount")]
        public int? CommunityRatingCount { get; set; }

        /// <summary>Whether the game supports cooperative play.</summary>
        [XmlElement("Cooperative")]
        public bool Cooperative { get; set; }

        /// <summary>LaunchBox database ID for this game.</summary>
        [XmlElement("DatabaseID")]
        public long? DatabaseID { get; set; }

        /// <summary>Developer of the game.</summary>
        [XmlElement("Developer")]
        [ForeignKey("Company")]
        public string? Developer { get; set; }

        /// <summary>Whether this is a DOS game.</summary>
        [XmlElement("DOS")]
        public bool DOS { get; set; }

        /// <summary>ESRB rating string.</summary>
        [XmlElement("ESRB")]
        [ForeignKey("ESRB")]
        public string? ESRB { get; set; }

        /// <summary>Semicolon-separated list of genres.</summary>
        [XmlElement("Genres")]
        public string? Genres { get; set; }

        /// <summary>Maximum number of players.</summary>
        [XmlElement("MaxPlayers")]
        public int? MaxPlayers { get; set; }

        /// <summary>Display name of the game.</summary>
        [XmlElement("Name")]
        public string? Name { get; set; }

        /// <summary>Description / synopsis.</summary>
        [XmlElement("Overview")]
        public string? Overview { get; set; }

        /// <summary>Platform the game belongs to.</summary>
        [XmlElement("Platform")]
        [ForeignKey("Platform", typeof(PlatformModel), "Name", "Id")]
        public string? Platform { get; set; }

        /// <summary>Publisher of the game.</summary>
        [XmlElement("Publisher")]
        [ForeignKey("Company")]
        public string? Publisher { get; set; }

        /// <summary>Original release date.</summary>
        [XmlElement("ReleaseDate")]
        public DateTime? ReleaseDate { get; set; }

        /// <summary>Release type (e.g. Released, Alpha, Beta).</summary>
        [XmlElement("ReleaseType")]
        [ForeignKey("ReleaseType")]
        public string? ReleaseType { get; set; }

        /// <summary>Four-digit release year.</summary>
        [XmlElement("ReleaseYear")]
        public int? ReleaseYear { get; set; }

        /// <summary>Setup executable file name, for DOS/PC games.</summary>
        [XmlElement("SetupFile")]
        public string? SetupFile { get; set; }

        /// <summary>MD5 hash of the setup file.</summary>
        [XmlElement("SetupMD5")]
        public string? SetupMD5 { get; set; }

        /// <summary>Startup executable file name.</summary>
        [XmlElement("StartupFile")]
        public string? StartupFile { get; set; }

        /// <summary>MD5 hash of the startup file.</summary>
        [XmlElement("StartupMD5")]
        public string? StartupMD5 { get; set; }

        /// <summary>Additional startup parameters.</summary>
        [XmlElement("StartupParameters")]
        public string? StartupParameters { get; set; }

        /// <summary>Steam App ID, if the game is on Steam.</summary>
        [XmlElement("SteamAppId")]
        public long? SteamAppId { get; set; }

        /// <summary>URL to a gameplay video.</summary>
        [XmlElement("VideoURL")]
        public string? VideoURL { get; set; }

        /// <summary>Wikipedia article URL.</summary>
        [XmlElement("WikipediaURL")]
        public string? WikipediaURL { get; set; }
    }
}
