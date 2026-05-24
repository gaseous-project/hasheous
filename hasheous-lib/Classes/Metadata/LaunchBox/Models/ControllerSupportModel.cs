using System.Xml.Serialization;
using hasheous_lib.Classes.Metadata;

namespace LaunchBox.Models
{
    /// <summary>LaunchBox MAME controller support entry for a ROM file.</summary>
    public class ControllerSupportModel
    {
        public static ModelIndexDefinition[] GetIndexes()
        {
            return new[]
            {
                ModelIndexDefinition.Single("IX_ControllerSupport_FileName", "FileName"),
                ModelIndexDefinition.Single("IX_ControllerSupport_ControllerName", "ControllerName"),
                ModelIndexDefinition.Composite("IX_ControllerSupport_FileName_ControllerName", false, "FileName", "ControllerName")
            };
        }

        /// <summary>Auto-increment identity; not present in XML.</summary>
        [XmlIgnore]
        public long Id { get; set; }

        /// <summary>Controller category (e.g. Joystick, Trackball).</summary>
        [XmlElement("ControllerCategory")]
        public string? ControllerCategory { get; set; }

        /// <summary>Controller name.</summary>
        [XmlElement("ControllerName")]
        public string? ControllerName { get; set; }

        /// <summary>ROM file name this controller entry applies to.</summary>
        [XmlElement("FileName")]
        public string? FileName { get; set; }

        /// <summary>Whether this controller is required for the game.</summary>
        [XmlElement("Required")]
        public bool Required { get; set; }
    }
}
