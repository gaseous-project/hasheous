using System.Data;
using Classes;

namespace hasheous_server.Models.Tasks
{
    /// <summary>
    /// Represents a single item in the background task queue.
    /// </summary>
    public class QueueItemModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueueItemModel"/> class.
        /// </summary>
        public QueueItemModel() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueItemModel"/> class from a <see cref="DataRow"/>.
        /// </summary>
        /// <param name="row">The <see cref="DataRow"/> containing the queue item data.</param>
        /// <remarks>This constructor calls the <see cref="Refresh(DataRow)"/> method to populate the properties.</remarks>
        public QueueItemModel(DataRow row)
        {
            Refresh(row);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueueItemModel"/> class with the specified task type, required capabilities, and optional parameters.
        /// </summary>
        /// <param name="taskType">The type of task to be performed.</param>
        /// <param name="requiredCapabilities">The list of required capabilities for this task.</param>
        /// <param name="parameters">Optional parameters for the task, serialized as a string.</param>
        public QueueItemModel(TaskType taskType, List<Capabilities> requiredCapabilities, string? parameters = null)
        {
            this._CreatedAt = DateTime.UtcNow;
            this._TaskName = taskType;
            this.Status = QueueItemStatus.Pending;
            this.RequiredCapabilities = requiredCapabilities;
            this.Parameters = parameters;

            // Id will be set when saved to database
            DataTable dt = Config.database.ExecuteCMD("INSERT INTO `Task_Queue` (`created_at`, `task_name`, `status`, `required_capabilities`, `parameters`) VALUES (@created_at, @task_name, @status, @required_capabilities, @parameters); SELECT LAST_INSERT_ID();", new Dictionary<string, object>
            {
                { "@created_at", this._CreatedAt },
                { "@task_name", this._TaskName.ToString() },
                { "@status", this.Status.ToString() },
                { "@required_capabilities", System.Text.Json.JsonSerializer.Serialize(this.RequiredCapabilities) },
                { "@parameters", this.Parameters }
            });
            this._Id = (long)dt.Rows[0][0];
        }

        /// <summary>
        /// Gets the unique identifier for the queue item.
        /// </summary>
        public long Id { get; }
        private long _Id { get; set; }

        /// <summary>
        /// Gets the date and time when the queue item was created.
        /// </summary>
        public DateTime CreatedAt { get; }
        private DateTime _CreatedAt { get; set; }

        /// <summary>
        /// Gets the type of task to be performed.
        /// </summary>
        public TaskType TaskName { get; }
        private TaskType _TaskName { get; set; }

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
        public List<Capabilities> RequiredCapabilities { get; } = new List<Capabilities>();
        private List<Capabilities> _RequiredCapabilities { get; set; } = new List<Capabilities>();

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

        /// <summary>
        /// Refreshes the properties of the queue item from the provided <see cref="DataRow"/>, or reloads from the database if no row is provided.
        /// </summary>
        /// <param name="row">The <see cref="DataRow"/> containing updated values, or null to reload from the database.</param>
        /// <returns>The refreshed <see cref="QueueItemModel"/> instance.</returns>
        public QueueItemModel Refresh(DataRow? row = null)
        {
            if (row == null)
            {
                DataTable dt = Config.database.ExecuteCMD("SELECT * FROM `Task_Queue` WHERE `id` = @id", new Dictionary<string, object>
                {
                    { "@id", this.Id }
                });

                if (dt.Rows.Count == 0)
                {
                    throw new Exception("Queue item not found in database.");
                }

                row = dt.Rows[0];
            }

            this._Id = (long)row.Field<long>("id");
            this._CreatedAt = row.Field<DateTime>("created_at");
            this._TaskName = (TaskType)Enum.Parse(typeof(TaskType), row.Field<string>("task_name"));
            this.Status = (QueueItemStatus)Enum.Parse(typeof(QueueItemStatus), row.Field<string>("status"));
            this.ClientId = row.IsNull("client_id") ? null : (long?)row.Field<long>("client_id");
            this.Parameters = row.IsNull("parameters") ? null : row.Field<string>("parameters");
            this.Result = row.IsNull("result") ? null : row.Field<string>("result");
            this.ErrorMessage = row.IsNull("error_message") ? null : row.Field<string>("error_message");
            this.StartedAt = row.IsNull("started_at") ? null : (DateTime?)row.Field<DateTime>("started_at");
            this.CompletedAt = row.IsNull("completed_at") ? null : (DateTime?)row.Field<DateTime>("completed_at");

            return this;
        }

        /// <summary>
        /// Terminates the queue item by setting its status to 'Cancelled' in the database.
        /// </summary>
        public async Task Terminate()
        {
            await Config.database.ExecuteCMDAsync("UPDATE `Task_Queue` SET `status` = @status WHERE `id` = @id", new Dictionary<string, object>
            {
                { "@id", this.Id },
                { "@status", QueueItemStatus.Cancelled }
            });
        }

        /// <summary>
        /// Commits the current state of the queue item to the database by updating its fields.
        /// </summary>
        public async Task Commit()
        {
            await Config.database.ExecuteCMDAsync("UPDATE `Task_Queue` SET `status` = @status, `client_id` = @client_id, `result` = @result, `error_message` = @error_message, `started_at` = @started_at, `completed_at` = @completed_at WHERE `id` = @id", new Dictionary<string, object>
            {
                { "@id", this.Id },
                { "@status", this.Status },
                { "@client_id", this.ClientId ?? (object)DBNull.Value },
                { "@result", this.Result ?? (object)DBNull.Value },
                { "@error_message", this.ErrorMessage ?? (object)DBNull.Value },
                { "@started_at", this.StartedAt ?? (object)DBNull.Value },
                { "@completed_at", this.CompletedAt?? (object)DBNull.Value }
            });
        }
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
        /// The task is currently in progress.
        /// </summary>
        InProgress = 10,
        /// <summary>
        /// The task has completed successfully.
        /// </summary>
        Completed = 20,
        /// <summary>
        /// The task has failed.
        /// </summary>
        Failed = 30,
        /// <summary>
        /// The task has been cancelled.
        /// </summary>
        Cancelled = 40
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
}