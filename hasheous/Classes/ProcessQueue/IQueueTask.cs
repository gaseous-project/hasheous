using static Classes.ProcessQueue.QueueProcessor;

namespace Classes.ProcessQueue
{
    /// <summary>
    /// Represents a task that can be queued and executed by the queue processor.
    /// </summary>
    public interface IQueueTask
    {
        /// <summary>
        /// Gets or sets the name of the task.
        /// </summary>
        string TaskName { get; set; }

        /// <summary>
        /// Executes the task asynchronously.
        /// </summary>
        Task<object?> ExecuteAsync();
    }
}