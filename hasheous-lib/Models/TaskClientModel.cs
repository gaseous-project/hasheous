namespace hasheous_server.Models.Tasks
{
    /// <summary>
    /// Represents a client that interacts with the task orchestration system.
    /// </summary>
    public class ClientModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the client.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the client.
        /// </summary>
        public required string ClientName { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the owner for the client.
        /// </summary>
        public required string OwnerId { get; set; }

        /// <summary>
        /// Gets or sets the API key assigned to the client.
        /// </summary>
        public string? APIKey { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the client was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the client last contacted the server.
        /// </summary>
        public DateTime LastContactAt { get; set; }

        /// <summary>
        /// Gets a value indicating whether the client is considered active (contacted within the last 5 minutes).
        /// </summary>
        public bool IsActive
        {
            get
            {
                return (DateTime.UtcNow - LastContactAt).TotalMinutes < 5;
            }
        }
    }
}