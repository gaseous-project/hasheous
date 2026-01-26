namespace hasheous_server.Models
{
    /// <summary>
    /// Model for reporting progress and status information.
    /// </summary>
    public class ReportModel
    {
        /// <summary>
        /// Gets or sets a dictionary of progress items keyed by name.
        /// </summary>
        public Dictionary<string, ProgressItem> Progress { get; set; } = new Dictionary<string, ProgressItem>();

        /// <summary>
        /// Represents an individual progress item with count, total, and description.
        /// </summary>
        public class ProgressItem
        {
            /// <summary>
            /// Gets or sets the current count.
            /// </summary>
            public int? count { get; set; }

            /// <summary>
            /// Gets or sets the total count.
            /// </summary>
            public int? total { get; set; }

            /// <summary>
            /// Gets or sets a description of the progress item.
            /// </summary>
            public string description { get; set; } = string.Empty;
        }
    }
}