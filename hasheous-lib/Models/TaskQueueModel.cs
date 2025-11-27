namespace hasheous_server.Models.Tasks
{
    public class QueueItemModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for the queue item.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the queue item was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the type of task to be performed.
        /// </summary>
        public TaskType TaskName { get; set; }

        /// <summary>
        /// Gets or sets the current status of the queue item.
        /// </summary>
        public QueueItemStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the client assigned to this task, if any.
        /// </summary>
        public long? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the parameters for the task, serialized as a string.
        /// </summary>
        public string? Parameters { get; set; }

        /// <summary>
        /// Gets or sets the result of the task, if available.
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        /// Gets or sets the error message if the task failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the task started, if available.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// Gets or sets the date and time when the task was completed, if available.
        /// </summary>
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Represents the status of a queue item.
    /// </summary>
    public enum QueueItemStatus
    {
        /// <summary>
        /// The task is pending and has not started yet.
        /// </summary>
        Pending,
        /// <summary>
        /// The task is currently in progress.
        /// </summary>
        InProgress,
        /// <summary>
        /// The task has completed successfully.
        /// </summary>
        Completed,
        /// <summary>
        /// The task has failed.
        /// </summary>
        Failed
    }

    /// <summary>
    /// Represents the type of task that can be queued.
    /// </summary>
    public enum TaskType
    {
        /// <summary>
        /// Task for generating AI descriptions and tagging.
        /// </summary>
        AIDescriptionsAndTagging
    }
}