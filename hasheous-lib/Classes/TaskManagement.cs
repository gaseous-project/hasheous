using System.Data;
using Classes;
using hasheous_server.Models;
using hasheous_server.Models.Tasks;
using static hasheous_server.Classes.DataObjects;

/// <summary>
/// Manages task orchestration and client interactions.
/// </summary>
namespace hasheous_server.Classes.Tasks.Clients
{
    /// <summary>
    /// Provides methods for managing client interactions.
    /// </summary>
    public static class ClientManagement
    {
        private static Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

        /// <summary>
        /// Registers a new client for the specified user API key and client name/version, returning the client API key and public client ID.
        /// </summary>
        /// <param name="userAPIKey">The user's API key.</param>
        /// <param name="clientName">The name of the client application.</param>
        /// <param name="version">The version of the client application.</param>
        /// <param name="capabilities">The list of supported capabilities for the client (optional).</param>
        /// <param name="publicId">The public client ID (optional). Used for existing clients.</param>
        /// <returns>A dictionary containing the client API key and public client ID.</returns>
        public static async Task<Dictionary<string, string>> RegisterClient(string userAPIKey, string clientName, string version, List<Capabilities>? capabilities = null, Guid? publicId = null)
        {
            // resolve userAPIKey to user account
            var user = GetUserObjectFromAPIKey(userAPIKey);

            // create new client
            string clientAPIKey = GenerateClientAPIKey();
            ClientModel client = new ClientModel(clientAPIKey, clientName, user.Id.ToString(), version, capabilities, publicId);
            Dictionary<string, string> response = new Dictionary<string, string>
            {
                { "client_api_key", clientAPIKey },
                { "client_id", client.PublicId.ToString() }
            };

            // build required capabilities list - this also provides basic configuration for each capability
            Dictionary<Capabilities, object> requiredCapabilities = new Dictionary<Capabilities, object>
            {
                { Capabilities.Internet, new Dictionary<string, object>
                    {
                        { "test_addresses", new List<string>{
                            "hasheous.org",
                            "1.1.1.1", "8.8.8.8"
                            }},
                        { "ping_attempts", 4 }
                    }
                },
                { Capabilities.DiskSpace, new Dictionary<string, object>
                    {
                        { "minimum_free_space_mb", 1024 } // require at least 1024 MB free space
                    }
                }
            };

            // encode required capabilities as JSON and return to the client
            response.Add("required_capabilities", System.Text.Json.JsonSerializer.Serialize(requiredCapabilities));

            // respond to the client
            return response;
        }

        /// <summary>
        /// Retrieves a client by its public ID.
        /// </summary>
        /// <param name="userAPIKey">The user's API key.</param>
        /// <param name="publicId">The public client ID.</param>
        /// <returns>The <see cref="ClientModel"/> if found; otherwise, null.</returns>
        /// <remarks>
        /// This method is intended for communication with the user.
        /// </remarks>
        public async static Task<ClientModel?> GetClient(string userAPIKey, string publicId)
        {
            // resolve userAPIKey to user account
            var user = GetUserObjectFromAPIKey(userAPIKey);

            DataTable dt = await db.ExecuteCMDAsync("SELECT * FROM Task_Clients WHERE public_id = @public_id AND owner_id = @owner_id LIMIT 1;", new Dictionary<string, object>
            {
                { "@public_id", publicId },
                { "@owner_id", user.Id }
            });
            if (dt.Rows.Count == 0)
            {
                return null;
            }
            return new ClientModel(dt.Rows[0]);
        }

        /// <summary>
        /// Retrieves a client by its API key and public client ID.
        /// </summary>
        /// <param name="clientAPIKey">The API key of the client.</param>
        /// <param name="publicId">The public client ID.</param>
        /// <returns>The <see cref="ClientModel"/> if found; otherwise, null.</returns>
        /// <remarks>
        /// This method is intended for internal system use only, such as during client heartbeats.
        /// </remarks>
        public async static Task<ClientModel?> GetClientByAPIKeyAndPublicId(string clientAPIKey, string publicId)
        {
            DataTable dt = await db.ExecuteCMDAsync("SELECT * FROM Task_Clients WHERE api_key = @api_key AND public_id = @public_id LIMIT 1;", new Dictionary<string, object>
            {
                { "@api_key", clientAPIKey },
                { "@public_id", publicId }
            });
            if (dt.Rows.Count == 0)
            {
                return null;
            }
            return new ClientModel(dt.Rows[0]);
        }

        /// <summary>
        /// Retrieves all registered clients for the specified user API key.
        /// </summary>
        /// <param name="userAPIKey">The user's API key.</param>
        /// <returns>A list of <see cref="ClientModel"/> objects associated with the user.</returns>
        /// <remarks>
        /// This method fetches all clients registered under the user account associated with the provided API key. This should be used for communication with the user.
        /// </remarks>
        public async static Task<List<ClientModel>> GetAllClientsForUser(string userAPIKey)
        {
            // resolve userAPIKey to user account
            var user = GetUserObjectFromAPIKey(userAPIKey);

            List<ClientModel> clients = new List<ClientModel>();
            DataTable dt = await db.ExecuteCMDAsync("SELECT * FROM Task_Clients WHERE owner_id = @owner_id;", new Dictionary<string, object>
            {
                { "@owner_id", user.Id }
            });
            foreach (DataRow row in dt.Rows)
            {
                clients.Add(new ClientModel(row));
            }
            return clients;
        }

        /// <summary>
        /// Retrieves all registered clients in the system.
        /// </summary>
        /// <returns>A list of all <see cref="ClientModel"/> objects.</returns>
        /// <remarks>
        /// This method fetches all clients in the system, regardless of ownership. This should be used for internal system operations only.
        /// </remarks>
        public async static Task<List<ClientModel>> GetAllClients()
        {
            List<ClientModel> clients = new List<ClientModel>();
            DataTable dt = await db.ExecuteCMDAsync("SELECT * FROM Task_Clients;");
            foreach (DataRow row in dt.Rows)
            {
                clients.Add(new ClientModel(row));
            }
            return clients;
        }

        /// <summary>
        /// Unregisters (removes) a client for the specified client API key and public client ID.
        /// </summary>
        /// <param name="userAPIKey">The user's API key.</param>
        /// <param name="publicId">The public client ID to unregister.</param>
        public async static Task UnregisterClient(string userAPIKey, string publicId)
        {
            var user = GetUserObjectFromAPIKey(userAPIKey);

            await db.ExecuteCMDAsync("DELETE FROM Task_Clients WHERE owner_id = @owner_id AND public_id = @public_id;", new Dictionary<string, object>
            {
                { "@owner_id", user.Id },
                { "@public_id", publicId }
            });
        }

        /// <summary>
        /// Updates the heartbeat timestamp for a client, indicating it is active.
        /// </summary>
        /// <param name="clientAPIKey">The API key of the client.</param>
        /// <param name="publicId">The public client ID.</param>
        /// <exception cref="Exception">Thrown if the client API key or public ID is invalid.</exception>
        public async static Task Heartbeat(string clientAPIKey, string publicId)
        {
            ClientModel? client = await GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
            if (client == null)
            {
                throw new Exception("Invalid client API key or public ID.");
            }
            await client.Heartbeat();
        }

        /// <summary>
        /// Updates the properties of a client, such as name, version, and capabilities.
        /// </summary>
        /// <param name="clientAPIKey">The API key of the client.</param>
        /// <param name="publicId">The public client ID.</param>
        /// <param name="clientName">The new name of the client application (optional).</param>
        /// <param name="version">The new version of the client application (optional).</param>
        /// <param name="capabilities">The new list of supported capabilities for the client (optional).</param>
        /// <exception cref="Exception">Thrown if the client API key or public ID is invalid.</exception>
        public async static Task UpdateClient(string clientAPIKey, string publicId, string? clientName = null, string? version = null, List<Capabilities>? capabilities = null)
        {
            ClientModel? client = await GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
            if (client == null)
            {
                throw new Exception("Invalid client API key or public ID.");
            }

            if (!string.IsNullOrEmpty(clientName))
            {
                client.ClientName = clientName;
            }
            if (!string.IsNullOrEmpty(version))
            {
                client.ClientVersion = version;
            }
            if (capabilities != null)
            {
                client.Capabilities = capabilities;
            }

            await client.Commit();
        }

        /// <summary>
        /// Retrieves the next available task for the specified client, or the currently assigned one if it exists.
        /// </summary>
        /// <param name="clientAPIKey">The API key of the client.</param>
        /// <param name="publicId">The public client ID.</param>
        public async static Task<QueueItemModel?> ClientGetTask(string clientAPIKey, string publicId)
        {
            ClientModel? client = await GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
            if (client == null)
            {
                throw new Exception("Invalid client API key or public ID.");
            }

            // find the next task suitable for this client - the task must match the client's capabilities
            string sql = @"SELECT tq.id AS id, tq.create_time AS create_time, tq.dataobjectid AS dataobjectid, tq.task_name AS task_name, tq.status AS status, tq.client_id AS client_id, tq.parameters AS parameters, tq.result AS result, tq.error_message AS error_message, tq.start_time AS start_time, tq.completion_time AS completion_time
                FROM Task_Queue tq
                LEFT JOIN Task_Queue_Capabilities tqc ON tq.id = tqc.task_queue_id
                WHERE tq.status = 0
                AND (tq.client_id IS NULL OR tq.client_id = @client_id)
                AND NOT EXISTS (
                    SELECT 1 
                    FROM Task_Queue_Capabilities tqc_required
                    WHERE tqc_required.task_queue_id = tq.id
                    AND tqc_required.capability_id NOT IN (" + string.Join(", ", client.Capabilities.Select(c => ((int)c).ToString())) + @")
                )
                GROUP BY tq.id
                ORDER BY tq.create_time ASC
                LIMIT 1;";
            DataTable dt = await db.ExecuteCMDAsync(sql, new Dictionary<string, object>
            {
                { "@client_id", client.Id }
            });
            if (dt.Rows.Count == 0)
            {
                // no task available
                return null;
            }

            QueueItemModel task = new QueueItemModel(dt.Rows[0]);

            // assign the task to this client if not already assigned
            if (task.ClientId == null || task.ClientId != client.Id)
            {
                task.ClientId = client.Id;
                task.Status = QueueItemStatus.Assigned;
                task.StartedAt = null;
                task.CompletedAt = null;
                task.Result = "";
                task.ErrorMessage = "";
                await task.Commit();
            }

            return task;
        }

        /// <summary>
        /// Submits the status or result of a task from a client.
        /// </summary>
        /// <param name="clientAPIKey">The API key of the client.</param>
        /// <param name="publicId">The public client ID.</param>
        /// <param name="taskId">The ID of the task being reported.</param>
        /// <param name="status">The status of the task.</param>
        /// <param name="result">The result or status of the task.</param>
        /// <param name="errorMessage">An optional error message if the task failed.</param>
        public async static Task ClientSubmitTaskStatusOrResult(string clientAPIKey, string publicId, string taskId, QueueItemStatus status, string result, string? errorMessage = null)
        {
            ClientModel? client = await GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
            if (client == null)
            {
                throw new Exception("Invalid client API key or public ID.");
            }

            QueueItemModel? task = TaskManagement.GetTask(long.Parse(taskId));
            if (task == null)
            {
                throw new Exception("Task not found.");
            }
            if (task.ClientId != client.Id)
            {
                throw new Exception("Task is not assigned to this client.");
            }
            task.Status = status;
            if (task.Status == QueueItemStatus.InProgress && task.StartedAt == null)
            {
                task.StartedAt = DateTime.UtcNow;
                task.CompletedAt = null;
            }
            task.Result = result;
            if (errorMessage != null)
            {
                task.ErrorMessage = errorMessage;
            }
            else
            {
                task.ErrorMessage = "";
            }
            await task.Commit();
        }

        /// <summary>
        /// Generates a new unique client API key.
        /// </summary>
        /// <returns>
        /// A 128 character hexadecimal string representing the client API key.
        /// </returns>
        private static string GenerateClientAPIKey()
        {
            const int maxAttempts = 10;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // 64 random bytes -> 128 hex characters
                byte[] bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(64);
                string key = Convert.ToHexString(bytes).ToLowerInvariant();

                var exists = db.ExecuteCMD(
                    "SELECT 1 FROM Task_Clients WHERE api_key = @api_key LIMIT 1;",
                    new Dictionary<string, object> { { "@api_key", key } }
                );

                if (exists.Rows.Count == 0)
                {
                    return key;
                }
            }

            // Extremely unlikely fallback
            throw new Exception("Unable to generate a unique client API key after multiple attempts.");
        }

        private static Authentication.ApplicationUser GetUserObjectFromAPIKey(string userAPIKey)
        {
            // resolve userAPIKey to user account
            Authentication.ApiKey apiKeyAuth = new Authentication.ApiKey();
            var user = apiKeyAuth.GetUserFromApiKey(userAPIKey);
            if (user == null)
            {
                throw new Exception("Invalid API Key");
            }

            return user;
        }
    }

    /// <summary>
    /// Provides methods for managing tasks and task orchestration.
    /// </summary>
    public static class TaskManagement
    {
        /// <summary>
        /// Enqueues a new task of the specified type with the given capabilities and parameters.
        /// </summary>
        /// <param name="dataObjectId">The identifier of the associated data object.</param>
        /// <param name="taskType">The type of the task to enqueue.</param>
        /// <param name="capabilities">A list of capabilities required to process the task.</param>
        /// <param name="parameters">A dictionary containing task-specific parameters.</param>
        /// <returns>The enqueued <see cref="QueueItemModel"/> instance.</returns>
        public static QueueItemModel EnqueueTask(long dataObjectId, TaskType taskType, List<Capabilities> capabilities, Dictionary<string, string>? parameters)
        {
            QueueItemModel task = new QueueItemModel(dataObjectId, taskType, capabilities, parameters);
            return task;
        }

        /// <summary>
        /// Enqueues a new task using default capabilities for Internet and DiskSpace and optional task-specific parameters.
        /// This overload will augment the provided parameters and capabilities based on the TaskType before delegating to the main EnqueueTask implementation.
        /// </summary>
        /// <param name="dataObjectId">The identifier of the associated data object.</param>
        /// <param name="taskType">The type of the task to enqueue.</param>
        /// <param name="parameters">Optional dictionary of task-specific parameters that may be modified or extended by the method.</param>
        /// <returns>The enqueued <see cref="QueueItemModel"/> instance.</returns>
        public static QueueItemModel? EnqueueTask(long dataObjectId, TaskType taskType, Dictionary<string, string>? parameters = null)
        {
            // default capabilities to Internet and DiskSpace
            List<Capabilities> capabilities = new List<Capabilities>
            {
                Capabilities.Internet,
                Capabilities.DiskSpace
            };

            if (parameters == null)
            {
                parameters = new Dictionary<string, string>();
            }

            switch (taskType)
            {
                case TaskType.AIDescriptionAndTagging:
                    capabilities.Add(Capabilities.AI);
                    Dictionary<string, string>? aiParams = BuildTaskParams(dataObjectId, taskType, parameters);
                    if (aiParams != null)
                    {
                        if (String.IsNullOrEmpty(aiParams["prompt"]))
                        {
                            // nothing to do
                            return null;
                        }
                        parameters = aiParams;
                    }
                    else
                    {
                        // nothing to do
                        return null;
                    }
                    break;
            }

            return EnqueueTask(dataObjectId, taskType, capabilities, parameters);
        }

        /// <summary>
        /// Dequeues (removes or terminates) the specified task by its ID.
        /// </summary>
        /// <param name="taskId">The ID of the task to dequeue.</param>
        /// <exception cref="Exception">Thrown if the task is not found.</exception>
        public static void DequeueTask(long taskId)
        {
            QueueItemModel? task = GetTask(taskId);
            if (task == null)
            {
                throw new Exception("Task not found.");
            }
            _ = task.Terminate();
        }

        /// <summary>
        /// Retrieves a task from the queue by its ID.
        /// </summary>
        /// <param name="taskId">The ID of the task to retrieve.</param>
        /// <returns>The <see cref="QueueItemModel"/> if found; otherwise, null.</returns>
        public static QueueItemModel? GetTask(long taskId)
        {
            DataTable dt = Config.database.ExecuteCMD("SELECT * FROM `Task_Queue` WHERE `id` = @id LIMIT 1;", new Dictionary<string, object>
            {
                { "@id", taskId }
            });
            if (dt.Rows.Count == 0)
            {
                return null;
            }
            return new QueueItemModel(dt.Rows[0]);
        }

        /// <summary>
        /// Retrieves all tasks currently in the task queue.
        /// </summary>
        /// <returns>A list of <see cref="QueueItemModel"/> representing all tasks in the queue.</returns>
        public static List<QueueItemModel> GetAllTasks()
        {
            return GetAllTasksInternal("SELECT * FROM `Task_Queue`;", new Dictionary<string, object>());
        }

        /// <summary>
        /// Retrieves all tasks in the queue for the specified data object.
        /// </summary>
        /// <param name="dataObjectId">The identifier of the data object whose tasks should be returned.</param>
        /// <returns>A list of <see cref="QueueItemModel"/> instances associated with the specified data object.</returns>
        public static List<QueueItemModel> GetAllTasks(long dataObjectId)
        {
            return GetAllTasksInternal("SELECT * FROM `Task_Queue` WHERE `dataobjectid` = @dataobjectid;", new Dictionary<string, object>
            {
                { "@dataobjectid", dataObjectId }
            });
        }

        private static List<QueueItemModel> GetAllTasksInternal(string query, Dictionary<string, object> parameters)
        {
            List<QueueItemModel> tasks = new List<QueueItemModel>();
            DataTable dt = Config.database.ExecuteCMD(query, parameters);
            foreach (DataRow row in dt.Rows)
            {
                tasks.Add(new QueueItemModel(row));
            }
            return tasks;
        }

        /// <summary>
        /// Updates the status, result, and error message of a specified task.
        /// </summary>
        /// <param name="taskId">The ID of the task to update.</param>
        /// <param name="status">The new status to set for the task.</param>
        /// <param name="result">The result of the task, if applicable (optional).</param>
        /// <param name="errorMessage">An error message if the task failed (optional).</param>
        /// <exception cref="Exception">Thrown if the task is not found.</exception>
        public static void UpdateTaskStatus(long taskId, QueueItemStatus status, string? result = null, string? errorMessage = null)
        {
            var task = GetTask(taskId);
            if (task == null)
            {
                throw new Exception("Task not found.");
            }
            task.Status = status;
            if (result != null)
            {
                task.Result = result;
            }
            if (errorMessage != null)
            {
                task.ErrorMessage = errorMessage;
            }
            _ = task.Commit();
        }

        public static Dictionary<string, string>? BuildTaskParams(long dataObjectId, TaskType taskType, Dictionary<string, string>? parameters = null)
        {
            if (parameters == null)
            {
                parameters = new Dictionary<string, string>();
            }

            switch (taskType)
            {
                case TaskType.AIDescriptionAndTagging:
                    parameters.Add("model", "gemma3");
                    string prompt = "";

                    // get the data object for use in prompt generation
                    var dataObjects = new DataObjects();
                    var dataObject = dataObjects.GetDataObject(dataObjectId).Result;
                    if (dataObject == null)
                    {
                        throw new Exception("Data object not found.");
                    }

                    switch (dataObject.ObjectType)
                    {
                        case DataObjectType.Game:
                            prompt = "Generate a detailed description and relevant tags for the game <DATA_OBJECT_NAME> for <DATA_OBJECT_PLATFORM>.\n\nThe description should be engaging and informative, highlighting key features and gameplay elements. Keep the description concise, ideally between 150 to 200 words.\n\nFor tags, compile a list of relevant keywords that accurately represent the game in the following categories: Genre, Gameplay, Features, Theme, Perspective, and Art Style. Ensure the tags are specific and commonly used within the gaming community, but avoid overly broad or generic terms.\n\nThe description and tags should be built from the following:\n\n<METADATA_SOURCE_DESCRIPTIONS>\n\nFormat the output as a raw JSON object with two properties: 'description' containing the generated description, and 'tags' containing an object with the specified categories as keys and arrays of corresponding tags as values.\n\nMake sure the JSON is properly structured and valid. Example output: {\"description\": \"<Generated Description>\", \"tags\": {\"Genre\": [\"Action\", \"Adventure\"], \"Gameplay\": [\"Open World\", \"Multiplayer\"], \"Features\": [\"Crafting\", \"Character Customization\"], \"Theme\": [\"Sci-Fi\", \"Fantasy\"], \"Perspective\": [\"First-Person\", \"Third-Person\"], \"Art Style\": [\"Realistic\", \"Pixel Art\"]}}.\n\nDo not include any additional text outside of the JSON object. Do not include any markdown formatting.";

                            // populate the prompt
                            prompt = prompt.Replace("<DATA_OBJECT_NAME>", dataObject.Name);
                            // get platform from metadata
                            DataObjectItem? itemPlatform = null;
                            if (dataObject.Attributes != null && dataObject.Attributes.Count > 0)
                            {
                                AttributeItem? platformAttribute = dataObject.Attributes.Find(x => x.attributeName == AttributeItem.AttributeName.Platform && x.attributeType == AttributeItem.AttributeType.ObjectRelationship);
                                if (platformAttribute != null)
                                {
                                    // get the associated platform dataobject
                                    if (platformAttribute.Value.GetType() == typeof(DataObjectItem))
                                    {
                                        itemPlatform = (DataObjectItem)platformAttribute.Value;
                                    }
                                    else
                                    {
                                        RelationItem relationItem = (RelationItem)platformAttribute.Value;
                                        itemPlatform = dataObjects.GetDataObject(DataObjectType.Platform, relationItem.relationId).Result;
                                    }
                                }
                            }
                            if (itemPlatform != null)
                            {
                                prompt = prompt.Replace("<DATA_OBJECT_PLATFORM>", itemPlatform.Name);
                            }
                            else
                            {
                                prompt = prompt.Replace("<DATA_OBJECT_PLATFORM>", "Unknown Platform");
                            }
                            // get metadata source descriptions
                            string metadataGameSourceDescriptions = "";
                            if (dataObject.Metadata != null && dataObject.Metadata.Count > 0)
                            {
                                string? sql = "";
                                DataTable? dt = null;
                                foreach (var metadataItem in dataObject.Metadata)
                                {
                                    switch (metadataItem.Source)
                                    {
                                        case Metadata.Communications.MetadataSources.IGDB:
                                            sql = "SELECT `summary` FROM `igdb`.`games` WHERE `id` = @id LIMIT 1;";
                                            dt = Config.database.ExecuteCMD(sql, new Dictionary<string, object>
                                            {
                                                { "@id", metadataItem.ImmutableId}
                                            });
                                            if (dt.Rows.Count > 0)
                                            {
                                                if (dt.Rows[0]["summary"] != DBNull.Value && dt.Rows[0]["summary"].ToString() != "")
                                                {
                                                    metadataGameSourceDescriptions += $"- IGDB: {dt.Rows[0]["summary"].ToString()}\n\n";
                                                }
                                            }
                                            break;
                                        case Metadata.Communications.MetadataSources.TheGamesDb:
                                            sql = "SELECT `overview` FROM `thegamesdb`.`games` WHERE `id` = @id LIMIT 1;";
                                            dt = Config.database.ExecuteCMD(sql, new Dictionary<string, object>
                                            {
                                                { "@id", metadataItem.ImmutableId}
                                            });
                                            if (dt.Rows.Count > 0)
                                            {
                                                if (dt.Rows[0]["overview"] != DBNull.Value && dt.Rows[0]["overview"].ToString() != "")
                                                {
                                                    metadataGameSourceDescriptions += $"- TheGamesDb: {dt.Rows[0]["overview"].ToString()}\n\n";
                                                }
                                            }
                                            break;
                                        case Metadata.Communications.MetadataSources.GiantBomb:
                                            sql = "SELECT `description` FROM `giantbomb`.`Game` WHERE `id` = @id LIMIT 1;";
                                            dt = Config.database.ExecuteCMD(sql, new Dictionary<string, object>
                                            {
                                                { "@id", metadataItem.ImmutableId}
                                            });
                                            if (dt.Rows.Count > 0)
                                            {
                                                if (dt.Rows[0]["description"] != DBNull.Value && dt.Rows[0]["description"].ToString() != "")
                                                {
                                                    metadataGameSourceDescriptions += $"- GiantBomb: {dt.Rows[0]["description"].ToString()}\n\n";
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                            prompt = prompt.Replace("<METADATA_SOURCE_DESCRIPTIONS>", metadataGameSourceDescriptions);
                            break;

                        case DataObjectType.Platform:
                            prompt = "Generate a detailed description for the gaming platform <DATA_OBJECT_NAME>.\n\nThe description should be engaging and informative, highlighting key features and historical significance. Keep the description concise, ideally between 100 to 150 words.\n\nFor tags, compile a list of relevant keywords that accurately represent the game in the following categories: Type, Era, Hardware Generation, Hardware Specs, Connectivity, Input Methods.\n\nEnsure the tags are specific and commonly used within the gaming community, but avoid overly broad or generic terms.\n\nThe description and tags should be built from the following:\n\n<METADATA_SOURCE_DESCRIPTIONS>\n\nFormat the output as a JSON object with two properties: 'description' containing the generated description, and 'tags' containing an object with the specified categories as keys and arrays of corresponding tags as values.\n\nMake sure the JSON is properly structured and valid. Example output: {\"description\": \"<Generated Description>\", \"tags\": {\"Type\": [\"Console\", \"Handheld\"], \"Era\": [\"Retro\", \"Modern\"], \"Hardware Generation\": [\"8th Gen\", \"9th Gen\"], \"Hardware Specs\": [\"4K Support\", \"VR Ready\"], \"Connectivity\": [\"Wi-Fi\", \"Bluetooth\"], \"Input Methods\": [\"Controller\", \"Touchscreen\"]}}.\n\nDo not include any additional text outside of the JSON object. Do not include any markdown formatting.";

                            // populate the prompt
                            prompt = prompt.Replace("<DATA_OBJECT_NAME>", dataObject.Name);
                            // get metadata source descriptions
                            string metadataSourcePlatformDescriptions = "";
                            if (dataObject.Metadata != null && dataObject.Metadata.Count > 0)
                            {
                                string? sql = "";
                                DataTable? dt = null;
                                foreach (var metadataItem in dataObject.Metadata)
                                {
                                    switch (metadataItem.Source)
                                    {
                                        case Metadata.Communications.MetadataSources.IGDB:
                                            sql = "SELECT `summary` FROM `igdb`.`platforms` WHERE `id` = @id LIMIT 1;";
                                            dt = Config.database.ExecuteCMD(sql, new Dictionary<string, object>
                                            {
                                                { "@id", metadataItem.ImmutableId}
                                            });
                                            if (dt.Rows.Count > 0)
                                            {
                                                if (dt.Rows[0]["summary"] != DBNull.Value && dt.Rows[0]["summary"].ToString() != "")
                                                {
                                                    {
                                                        metadataSourcePlatformDescriptions += $"- IGDB: {dt.Rows[0]["summary"].ToString()}\n\n";
                                                    }
                                                }
                                            }
                                            break;
                                        case Metadata.Communications.MetadataSources.TheGamesDb:
                                            sql = "SELECT `overview` FROM `thegamesdb`.`games` WHERE `id` = @id LIMIT 1;";
                                            dt = Config.database.ExecuteCMD(sql, new Dictionary<string, object>
                                            {
                                                { "@id", metadataItem.ImmutableId}
                                            });
                                            if (dt.Rows.Count > 0)
                                            {
                                                if (dt.Rows[0]["overview"] != DBNull.Value && dt.Rows[0]["overview"].ToString() != "")
                                                {
                                                    metadataSourcePlatformDescriptions += $"- TheGamesDb: {dt.Rows[0]["overview"].ToString()}\n\n";
                                                }
                                            }
                                            break;
                                        case Metadata.Communications.MetadataSources.GiantBomb:
                                            sql = "SELECT `description` FROM `giantbomb`.`Game` WHERE `id` = @id LIMIT 1;";
                                            dt = Config.database.ExecuteCMD(sql, new Dictionary<string, object>
                                            {
                                                { "@id", metadataItem.ImmutableId}
                                            });
                                            if (dt.Rows.Count > 0)
                                            {
                                                if (dt.Rows[0]["description"] != DBNull.Value && dt.Rows[0]["description"].ToString() != "")
                                                {
                                                    metadataSourcePlatformDescriptions += $"- GiantBomb: {dt.Rows[0]["description"].ToString()}\n\n";
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                            if (metadataSourcePlatformDescriptions == "")
                            {
                                // no descriptions found - nothing to do
                                return null;
                            }
                            prompt = prompt.Replace("<METADATA_SOURCE_DESCRIPTIONS>", metadataSourcePlatformDescriptions);
                            break;

                        default:
                            prompt = "";
                            break;
                    }
                    parameters.Add("prompt", prompt);
                    break;
            }

            return parameters;
        }
    }
}