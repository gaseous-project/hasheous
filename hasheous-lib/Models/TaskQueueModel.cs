using System.Data;
using System.Threading.Tasks;
using Classes;
using hasheous_server.Classes.Tasks.Clients;

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
        /// <param name="dataObjectId">The identifier of the associated data object.</param>
        /// <param name="taskType">The type of task to be performed.</param>
        /// <param name="requiredCapabilities">The list of required capabilities for this task.</param>
        /// <param name="parameters">Optional parameters for the task.</param>
        public QueueItemModel(long dataObjectId, TaskType taskType, List<Capabilities> requiredCapabilities, Dictionary<string, string>? parameters = null)
        {
            this._CreatedAt = DateTime.UtcNow;
            this._DataObjectId = dataObjectId;
            this._TaskName = taskType;
            this.Status = QueueItemStatus.Pending;
            this._RequiredCapabilities = requiredCapabilities;
            this.Parameters = parameters;

            // Id will be set when saved to database
            DataTable dt = Config.database.ExecuteCMD("INSERT INTO `Task_Queue` (`create_time`, `dataobjectid`, `task_name`, `status`, `required_capabilities`, `parameters`) VALUES (@created_at, @dataobjectid, @task_name, @status, @required_capabilities, @parameters); SELECT LAST_INSERT_ID();", new Dictionary<string, object>
            {
                { "@created_at", this._CreatedAt },
                { "@dataobjectid", this._DataObjectId },
                { "@task_name", (int)this._TaskName },
                { "@status", (int)this.Status },
                { "@required_capabilities", System.Text.Json.JsonSerializer.Serialize(this.RequiredCapabilities) },
                { "@parameters", System.Text.Json.JsonSerializer.Serialize(this.Parameters) ?? "{}"}
            });
            foreach (Capabilities capability in requiredCapabilities)
            {
                Config.database.ExecuteCMD("INSERT INTO `Task_Queue_Capabilities` (`task_queue_id`, `capability_id`) VALUES (@task_queue_id, @capability_id);", new Dictionary<string, object>
                {
                    { "@task_queue_id", dt.Rows[0][0] },
                    { "@capability_id", (int)capability }
                });
            }
            this._Id = Convert.ToInt64(dt.Rows[0][0]);
        }

        /// <summary>
        /// Gets the unique identifier for the queue item.
        /// </summary>
        public long Id { get { return _Id; } }
        private long _Id { get; set; }

        /// <summary>
        /// Gets the date and time when the queue item was created.
        /// </summary>
        public DateTime CreatedAt { get { return _CreatedAt; } }
        private DateTime _CreatedAt { get; set; }

        /// <summary>
        /// Gets the identifier of the associated data object.
        /// </summary>
        public long DataObjectId { get { return _DataObjectId; } }
        private long _DataObjectId { get; set; }

        /// <summary>
        /// Gets the type of task to be performed.
        /// </summary>
        public TaskType TaskName { get { return _TaskName; } }
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
        public List<Capabilities> RequiredCapabilities { get { return _RequiredCapabilities; } }
        private List<Capabilities> _RequiredCapabilities { get; set; } = new List<Capabilities>();

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

        /// <summary>
        /// Resets transient runtime fields on the queue item so it can be re-queued or retried.
        /// </summary>
        public async Task Reset()
        {
            this.Status = QueueItemStatus.Pending;
            this.ClientId = null;
            this.Result = null;
            this.ErrorMessage = null;
            this.StartedAt = null;
            this.CompletedAt = null;

            this.Parameters = TaskManagement.BuildTaskParams(this.DataObjectId, this.TaskName);

            switch (this.TaskName)
            {
                case TaskType.AIDescriptionAndTagging:
                    if (string.IsNullOrEmpty(this.Parameters?["prompt_description"]) && string.IsNullOrEmpty(this.Parameters?["prompt_tagging"]))
                    {
                        // nothing to do - terminate the task
                        await this.Terminate();
                    }
                    break;
            }

            await this.Commit();
        }

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
            this._CreatedAt = row.Field<DateTime>("create_time");
            this._DataObjectId = (long)row.Field<long>("dataobjectid");
            this._TaskName = (TaskType)row.Field<int>("task_name");
            this.Status = (QueueItemStatus)row.Field<int>("status");
            this.ClientId = row.IsNull("client_id") ? null : (long?)row.Field<long>("client_id");
            this.Parameters = row.IsNull("parameters") ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(row.Field<string>("parameters") ?? "{}");
            this.Result = row.IsNull("result") ? null : row.Field<string>("result");
            this.ErrorMessage = row.IsNull("error_message") ? null : row.Field<string>("error_message");
            this.StartedAt = row.IsNull("start_time") ? null : (DateTime?)row.Field<DateTime>("start_time");
            this.CompletedAt = row.IsNull("completion_time") ? null : (DateTime?)row.Field<DateTime>("completion_time");

            // Load required capabilities from the database
            DataTable capabilitiesDt = Config.database.ExecuteCMD("SELECT `capability_id` FROM `Task_Queue_Capabilities` WHERE `task_queue_id` = @task_queue_id", new Dictionary<string, object>
            {
                { "@task_queue_id", this.Id }
            });

            this._RequiredCapabilities = new List<Capabilities>();
            foreach (DataRow capabilityRow in capabilitiesDt.Rows)
            {
                this.RequiredCapabilities.Add((Capabilities)(int)capabilityRow["capability_id"]);
            }

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
            await Config.database.ExecuteCMDAsync("UPDATE `Task_Queue` SET `status` = @status, `client_id` = @client_id, `result` = @result, `error_message` = @error_message, `start_time` = @started_at, `completion_time` = @completed_at WHERE `id` = @id", new Dictionary<string, object>
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
}