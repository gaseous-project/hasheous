using System.Threading;
using System.Threading.Tasks;

namespace hasheous_taskrunner.Classes.Tasks
{
    /// <summary>
    /// Represents a task that can be executed by the task runner.
    /// </summary>
    public interface ITask
    {
        /// <summary>
        /// Gets the type of the task.
        /// </summary>
        public TaskType TaskType { get; }

        /// <summary>
        /// Verifies the task parameters asynchronously.
        /// </summary>
        /// <param name="parameters">A dictionary of parameters required for task verification.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the verification to complete.</param>
        /// <returns>A TaskVerificationResult indicating the result of the verification.</returns>
        public Task<TaskVerificationResult> VerifyAsync(Dictionary<string, string>? parameters, CancellationToken cancellationToken);

        /// <summary>
        /// Executes the task asynchronously.
        /// </summary>
        /// <param name="parameters">A dictionary of parameters required for task execution.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>A dictionary containing the results of the task execution.</returns>
        public Task<Dictionary<string, object>> ExecuteAsync(Dictionary<string, string>? parameters, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Represents a task item in the task queue.
    /// </summary>
    public class TaskItem
    {
        /// <summary>
        /// Gets the unique identifier for the queue item.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets the date and time when the queue item was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets the identifier of the associated data object.
        /// </summary>
        public long DataObjectId { get; set; }

        /// <summary>
        /// Gets the type of task to be performed.
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
        /// Gets the list of required capabilities (task types) for this queue item.
        /// </summary>
        public List<Capabilities> RequiredCapabilities { get; set; } = new List<Capabilities>();

        /// <summary>
        /// Gets or sets the parameters for the task.
        /// </summary>
        public Dictionary<string, string>? Parameters { get; set; }

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
        Pending = 0,
        /// <summary>
        /// The task has been assigned to a client.
        /// </summary>
        Assigned = 10,
        /// <summary>
        /// The task is currently in progress.
        /// </summary>
        InProgress = 20,
        /// <summary>
        /// The task has been submitted.
        /// </summary>
        Submitted = 30,
        /// <summary>
        /// The task has completed successfully.
        /// </summary>
        Completed = 40,
        /// <summary>
        /// The task has failed.
        /// </summary>
        Failed = 50,
        /// <summary>
        /// The task has been cancelled.
        /// </summary>
        Cancelled = 60
    }

    /// <summary>
    /// Represents the type of task that can be queued.
    /// </summary>
    public enum TaskType
    {
        /// <summary>
        /// Task for AI description generation and tagging.
        /// </summary>
        AIDescriptionAndTagging = 0
    }

    /// <summary>
    /// Represents the capabilities that a client or worker can have for processing tasks.
    /// </summary>
    public enum Capabilities
    {
        /// <summary>
        /// Capability for accessing the Internet. All clients have this capability by default.
        /// </summary>
        Internet = 0,

        /// <summary>
        /// Capability for ensuring sufficient disk space is available.
        /// </summary>
        DiskSpace = 10,

        /// <summary>
        /// Capability for handling AI tasks.
        /// </summary>
        AI = 20
    }

    /// <summary>
    /// Represents the result of task verification.
    /// </summary>
    public class TaskVerificationResult
    {
        /// <summary>
        /// Gets or sets the verification status.
        /// </summary>
        public VerificationStatus Status { get; set; } = VerificationStatus.Success;

        /// <summary>
        /// Gets or sets the details of the verification.
        /// </summary>
        public Dictionary<string, string> Details { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Represents the status of task verification.
        /// </summary>
        public enum VerificationStatus
        {
            /// <summary>
            /// The verification was successful.
            /// </summary>
            Success,

            /// <summary>
            /// The verification failed.
            /// </summary>
            Failure
        }
    }
}