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
            /// Gets or sets a value indicating whether ETA calculation is enabled for this progress item.
            /// </summary>
            public bool enableETACalculation { get; set; } = false;

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

            /// <summary>
            /// Gets or sets the processing speed (items per second).
            /// Calculated only when count and total are both non-null and non-zero.
            /// </summary>
            public double? itemsPerSecond { get; set; }

            /// <summary>
            /// Gets or sets the estimated time to completion in seconds.
            /// Calculated only when count and total are both non-null and non-zero.
            /// </summary>
            public double? estimatedSecondsRemaining { get; set; }

            /// <summary>
            /// Gets or sets the timestamp when this progress was first tracked.
            /// Used internally for speed calculations.
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore]
            [Newtonsoft.Json.JsonIgnore]
            public DateTime? firstTrackedTime { get; set; }

            /// <summary>
            /// Gets or sets the initial count when tracking began.
            /// Used internally for speed calculations.
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore]
            [Newtonsoft.Json.JsonIgnore]
            public int? firstTrackedCount { get; set; }
        }
    }
}