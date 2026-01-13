using System.Data;
using System.Reflection;
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

            // check roles
            var userRolesTable = new Authentication.UserRolesTable(db);
            var userRoles = userRolesTable.FindByUserId(user.Id);
            if (!userRoles.Contains("Task Runner"))
            {
                throw new Exception("User does not have the required 'Task Runner' role.");
            }

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
            // create cache key
            string cacheKey = hasheous.Classes.RedisConnection.GenerateKey("TaskWorkerAPIKeys", clientAPIKey + publicId);

            // // check cache first
            // if (Config.RedisConfiguration.Enabled)
            // {
            //     string? cachedValue = hasheous.Classes.RedisConnection.GetDatabase(0).StringGet(cacheKey);
            //     if (cachedValue != null)
            //     {
            //         ClientModel? cachedItem = Newtonsoft.Json.JsonConvert.DeserializeObject<ClientModel>(cachedValue);

            //         return cachedItem;
            //     }
            // }

            DataTable dt = await db.ExecuteCMDAsync("SELECT * FROM Task_Clients WHERE api_key = @api_key AND public_id = @public_id LIMIT 1;", new Dictionary<string, object>
            {
                { "@api_key", clientAPIKey },
                { "@public_id", publicId }
            });
            if (dt.Rows.Count == 0)
            {
                return null;
            }
            else
            {
                // // cache the result
                // hasheous.Classes.RedisConnection.GetDatabase(0).StringSet(cacheKey, Newtonsoft.Json.JsonConvert.SerializeObject(new ClientModel(dt.Rows[0])), TimeSpan.FromSeconds(3600)); // cache for 1 hour
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
        /// Retrieves all registered clients for the specified user ID.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <returns>A list of <see cref="ClientModel"/> objects associated with the user.</returns>
        /// <remarks>
        /// This method fetches all clients registered under the specified user ID.
        /// </remarks>
        public async static Task<List<ClientModel>> GetAllClientsForUserId(string userId)
        {
            List<ClientModel> clients = new List<ClientModel>();
            DataTable dt = await db.ExecuteCMDAsync("SELECT * FROM Task_Clients WHERE owner_id = @owner_id;", new Dictionary<string, object>
            {
                { "@owner_id", userId }
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
            if (task.Status == QueueItemStatus.Submitted || task.Status == QueueItemStatus.Failed)
            {
                task.CompletedAt = DateTime.UtcNow;
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
                        if (!aiParams.ContainsKey("prompt_description") || !aiParams.ContainsKey("prompt_tags") || !aiParams.ContainsKey("sources") ||
                            String.IsNullOrEmpty(aiParams["prompt_description"]) && String.IsNullOrEmpty(aiParams["prompt_tags"]) && String.IsNullOrEmpty(aiParams["sources"]))
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

        public static QueueItemStatus? GetClientTaskStatus(long clientId)
        {
            List<int> activeStatuses = new List<int>
            {
                (int)QueueItemStatus.Assigned,
                (int)QueueItemStatus.InProgress
            };

            DataTable dt = Config.database.ExecuteCMD($"SELECT * FROM `Task_Queue` WHERE `client_id` = @client_id AND `status` IN ({string.Join(",", activeStatuses)}) LIMIT 1;", new Dictionary<string, object>
            {
                { "@client_id", clientId }
            });
            if (dt.Rows.Count == 0)
            {
                return null;
            }
            return (QueueItemStatus?)dt.Rows[0]["status"];
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
                    parameters.Add("model_description", "gemma3:4b");
                    parameters.Add("model_tags", "gemma3:4b");
                    parameters.Add("sources", "");  // will be populated with actual sources used

                    string prompt_description = "";
                    string prompt_tags = "";

                    // get the data object for use in prompt generation
                    var dataObjects = new DataObjects();
                    var dataObject = dataObjects.GetDataObject(dataObjectId).Result;
                    if (dataObject == null)
                    {
                        throw new Exception("Data object not found.");
                    }

                    // supply the sources and prompts based on data object type
                    switch (dataObject.ObjectType)
                    {
                        case DataObjectType.Game:
                            // load the prompts
                            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("hasheous_lib.Support.AIGameDescriptionPrompt.txt"))
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    prompt_description = reader.ReadToEnd();
                                }
                            }
                            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("hasheous_lib.Support.AIGameTagPrompt.txt"))
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    prompt_tags = reader.ReadToEnd();
                                }
                            }

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
                            prompt_description = prompt_description.Replace("<DATA_OBJECT_NAME>", dataObject.Name);
                            prompt_tags = prompt_tags.Replace("<DATA_OBJECT_NAME>", dataObject.Name);
                            if (itemPlatform != null)
                            {
                                prompt_description = prompt_description.Replace("<DATA_OBJECT_PLATFORM>", itemPlatform.Name);
                                prompt_tags = prompt_tags.Replace("<DATA_OBJECT_PLATFORM>", itemPlatform.Name);
                            }
                            else
                            {
                                prompt_description = prompt_description.Replace("<DATA_OBJECT_PLATFORM>", "Unknown Platform");
                                prompt_tags = prompt_tags.Replace("<DATA_OBJECT_PLATFORM>", "Unknown Platform");
                            }
                            // populate the prompt
                            parameters.Add("prompt_description", prompt_description);
                            parameters.Add("prompt_tags", prompt_tags);
                            // get metadata source descriptions
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
                                                    parameters.Add("Source_IGDB", dt.Rows[0]["summary"].ToString() ?? "");
                                                    parameters["sources"] += "IGDB; ";
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
                                                    parameters.Add("Source_TheGamesDb", dt.Rows[0]["overview"].ToString() ?? "");
                                                    parameters["sources"] += "TheGamesDb; ";
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
                                                    parameters.Add("Source_GiantBomb", dt.Rows[0]["description"].ToString() ?? "");
                                                    parameters["sources"] += "GiantBomb; ";
                                                }
                                            }
                                            break;
                                        case Metadata.Communications.MetadataSources.Wikipedia:
                                            // download the Wikipedia summary for the game
                                            if (metadataItem.ImmutableId == null || metadataItem.ImmutableId == "")
                                            {
                                                break;
                                            }
                                            string? wikiContent = GetWikipediaContent(metadataItem.ImmutableId);
                                            if (wikiContent != "")
                                            {
                                                parameters.Add("Source_Wikipedia", wikiContent ?? "");
                                                parameters["sources"] += "Wikipedia; ";
                                            }
                                            break;
                                    }
                                }
                            }
                            if (parameters["sources"] == "")
                            {
                                // no descriptions found - nothing to do
                                return null;
                            }
                            break;

                        case DataObjectType.Platform:
                            // load the prompts
                            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("hasheous_lib.Support.AIPlatformDescriptionPrompt.txt"))
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    prompt_description = reader.ReadToEnd();
                                }
                            }
                            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("hasheous_lib.Support.AIPlatformTagPrompt.txt"))
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    prompt_tags = reader.ReadToEnd();
                                }
                            }

                            // populate the prompt
                            parameters.Add("prompt_description", prompt_description.Replace("<DATA_OBJECT_NAME>", dataObject.Name));
                            parameters.Add("prompt_tags", prompt_tags.Replace("<DATA_OBJECT_NAME>", dataObject.Name));
                            // get metadata source descriptions
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
                                                        parameters.Add("Source_IGDB", dt.Rows[0]["summary"].ToString() ?? "");
                                                        parameters["sources"] += "IGDB; ";
                                                    }
                                                }
                                            }
                                            break;
                                        case Metadata.Communications.MetadataSources.TheGamesDb:
                                            sql = "SELECT `overview` FROM `thegamesdb`.`platforms` WHERE `id` = @id LIMIT 1;";
                                            dt = Config.database.ExecuteCMD(sql, new Dictionary<string, object>
                                            {
                                                { "@id", metadataItem.ImmutableId}
                                            });
                                            if (dt.Rows.Count > 0)
                                            {
                                                if (dt.Rows[0]["overview"] != DBNull.Value && dt.Rows[0]["overview"].ToString() != "")
                                                {
                                                    parameters.Add("Source_TheGamesDb", dt.Rows[0]["overview"].ToString() ?? "");
                                                    parameters["sources"] += "TheGamesDb; ";
                                                }
                                            }
                                            break;
                                        case Metadata.Communications.MetadataSources.GiantBomb:
                                            sql = "SELECT `description` FROM `giantbomb`.`Platform` WHERE `id` = @id LIMIT 1;";
                                            dt = Config.database.ExecuteCMD(sql, new Dictionary<string, object>
                                            {
                                                { "@id", metadataItem.ImmutableId}
                                            });
                                            if (dt.Rows.Count > 0)
                                            {
                                                if (dt.Rows[0]["description"] != DBNull.Value && dt.Rows[0]["description"].ToString() != "")
                                                {
                                                    parameters.Add("Source_GiantBomb", dt.Rows[0]["description"].ToString() ?? "");
                                                    parameters["sources"] += "GiantBomb; ";
                                                }
                                            }
                                            break;
                                        case Metadata.Communications.MetadataSources.Wikipedia:
                                            // download the Wikipedia summary for the game
                                            if (metadataItem.ImmutableId == null || metadataItem.ImmutableId == "")
                                            {
                                                break;
                                            }
                                            string? wikiContent = GetWikipediaContent(metadataItem.ImmutableId);
                                            if (wikiContent != "")
                                            {
                                                parameters.Add("Source_Wikipedia", wikiContent ?? "");
                                                parameters["sources"] += "Wikipedia; ";
                                            }
                                            break;
                                    }
                                }
                            }
                            if (parameters["sources"] == "")
                            {
                                // no descriptions found - nothing to do
                                return null;
                            }
                            break;

                        default:
                            break;
                    }
                    break;
            }

            return parameters;
        }

        private static int WikiCallsInLastSecond = 0;
        private static DateTime LastWikiCallTime = DateTime.MinValue;

        private static bool IsLikelyUrlEncoded(string path)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(path, @"%[0-9A-Fa-f]{2}");
        }

        private static string GetWikipediaContent(string address)
        {
            // Try to parse the address as-is first
            if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? wikiUri))
            {
                // If that fails, try to fix the URL by encoding the path parts
                // Extract scheme and authority (e.g., "https://en.wikipedia.org") from the address
                var match = System.Text.RegularExpressions.Regex.Match(address, @"^(https?://[^/]+)(.*)$");
                if (match.Success)
                {
                    string schemeAndHost = match.Groups[1].Value;
                    string path = match.Groups[2].Value;

                    // Only encode if not already encoded
                    if (!IsLikelyUrlEncoded(path))
                    {
                        // Encode path segments individually to preserve forward slashes
                        string[] pathParts = path.Split('/');
                        for (int i = 0; i < pathParts.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(pathParts[i]))
                            {
                                pathParts[i] = System.Net.WebUtility.UrlEncode(pathParts[i]);
                            }
                        }
                        path = string.Join("/", pathParts);
                    }

                    string fixedAddress = schemeAndHost + path;
                    if (!Uri.TryCreate(fixedAddress, UriKind.Absolute, out wikiUri))
                    {
                        // still invalid - skip
                        return "";
                    }
                }
                else
                {
                    // invalid uri format - skip
                    return "";
                }
            }

            // extract the page name from the uri
            string pageName = wikiUri.AbsolutePath.Replace("/wiki/", "");

            // ensure the page name is URL encoded (in case it wasn't already)
            if (!IsLikelyUrlEncoded(pageName))
            {
                pageName = System.Net.WebUtility.UrlEncode(pageName);
            }

            // build the Wikipedia API url and download the html content
            string wikiApiUrl = $"https://{wikiUri.Host}/api/rest_v1/page/html/{pageName}";

            // implement simple rate limiting: Wikipedia allows up to 200 requests per second from a single IP
            DateTime now = DateTime.UtcNow;
            if ((now - LastWikiCallTime).TotalSeconds < 1)
            {
                WikiCallsInLastSecond++;
                if (WikiCallsInLastSecond >= 190)
                {
                    // wait for a second
                    System.Threading.Thread.Sleep(1000);
                    WikiCallsInLastSecond = 0;
                    LastWikiCallTime = DateTime.UtcNow;
                }
            }
            else
            {
                WikiCallsInLastSecond = 1;
                LastWikiCallTime = now;
            }

            // fetch the data
            using (var client = new System.Net.Http.HttpClient())
            {
                // set the user agent
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Hasheous/1.0");

                var response = client.GetAsync(wikiApiUrl).Result;
                if (response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsStringAsync().Result;
                    if (content.Length > 0)
                    {
                        // the content is html - we need to extract the text content and remove any unneeded sections, and convert to a markdown-like format
                        var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                        htmlDoc.LoadHtml(content);

                        // remove all images - images are in "figure" elements
                        var figureNodes = htmlDoc.DocumentNode.SelectNodes("//figure"); ;
                        if (figureNodes != null)
                        {
                            foreach (var figureNode in figureNodes)
                            {
                                figureNode.Remove();
                            }
                        }

                        // get the infobox table and convert it to a markdown table
                        string markdownTable = "";
                        var infoboxNodeForTable = htmlDoc.DocumentNode.SelectSingleNode("//table[contains(@class, 'infobox')]");
                        if (infoboxNodeForTable != null)
                        {
                            var rows = infoboxNodeForTable.SelectNodes(".//tr");
                            if (rows != null)
                            {
                                List<string> tableLines = new List<string>();
                                foreach (var row in rows)
                                {
                                    var headerCell = row.SelectSingleNode(".//th");
                                    var dataCell = row.SelectSingleNode(".//td");
                                    if (headerCell != null && dataCell != null)
                                    {
                                        string headerText = headerCell.InnerText.Trim().Replace("\n", " ");
                                        string dataText = dataCell.InnerText.Trim().Replace("\n", " ");
                                        tableLines.Add($"| **{headerText}** | {dataText} |");
                                    }
                                }
                                if (tableLines.Count > 0)
                                {
                                    // add header separator
                                    tableLines.Insert(1, "| --- | --- |");
                                    markdownTable = string.Join("\n", tableLines);
                                }
                            }
                        }

                        // remove the infobox table and everything before it in the first section - infobox is a "table" element with class "infobox"
                        // replace the infobox with the markdown table if it exists
                        var infoboxNode = htmlDoc.DocumentNode.SelectSingleNode("//table[contains(@class, 'infobox')]");
                        if (markdownTable != "" && infoboxNode != null)
                        {
                            var markdownNode = htmlDoc.CreateTextNode(markdownTable + "\n\n");
                            infoboxNode.ParentNode.ReplaceChild(markdownNode, infoboxNode);
                        }

                        if (infoboxNode != null)
                        {
                            var firstSection = infoboxNode.ParentNode; ;
                            if (firstSection != null)
                            {
                                var nodesToRemove = new List<HtmlAgilityPack.HtmlNode>();
                                foreach (var childNode in firstSection.ChildNodes)
                                {
                                    nodesToRemove.Add(childNode);
                                    if (childNode == infoboxNode)
                                    {
                                        break;
                                    }
                                }
                                foreach (var node in nodesToRemove)
                                {
                                    node.Remove();
                                }
                            }
                        }

                        // remove all superscript elements - these are used for citations
                        var superscriptNodes = htmlDoc.DocumentNode.SelectNodes("//sup"); ;
                        if (superscriptNodes != null)
                        {
                            foreach (var supNode in superscriptNodes)
                            {
                                supNode.Remove();
                            }
                        }

                        // convert all links to plain text - links are "a" elements
                        var linkNodes = htmlDoc.DocumentNode.SelectNodes("//a"); ;
                        if (linkNodes != null)
                        {
                            foreach (var linkNode in linkNodes)
                            {
                                var textNode = htmlDoc.CreateTextNode(linkNode.InnerText);
                                linkNode.ParentNode.ReplaceChild(textNode, linkNode);
                            }
                        }

                        // remove references section - references section is a "section" element where the first child is an "h2" with id "References"
                        var referencesSection = htmlDoc.DocumentNode.SelectSingleNode("//section[h2[@id='References']]");
                        if (referencesSection != null)
                        {
                            referencesSection.Remove();
                        }

                        // remove external links section - external links section is a "section" element where the first child is an "h2" with id "External_links"
                        var externalLinksSection = htmlDoc.DocumentNode.SelectSingleNode("//section[h2[@id='External_links']]");
                        if (externalLinksSection != null)
                        {
                            externalLinksSection.Remove();
                        }

                        // replace h1-h6 tags with their inner text prefixed with "#" corresponding to their level and suffixed with double newlines
                        for (int i = 1; i <= 6; i++)
                        {
                            var headerNodes = htmlDoc.DocumentNode.SelectNodes($"//h{i}"); ;
                            if (headerNodes != null)
                            {
                                foreach (var headerNode in headerNodes)
                                {
                                    var textNode = htmlDoc.CreateTextNode(new string('#', i) + " " + headerNode.InnerText + "\n\n");
                                    headerNode.ParentNode.ReplaceChild(textNode, headerNode);
                                }
                            }
                        }

                        // replace br tags with single newlines
                        var brNodes = htmlDoc.DocumentNode.SelectNodes("//br"); ;
                        if (brNodes != null)
                        {
                            foreach (var brNode in brNodes)
                            {
                                var textNode = htmlDoc.CreateTextNode("\n");
                                brNode.ParentNode.ReplaceChild(textNode, brNode);
                            }
                        }

                        // replace p tags with double newlines
                        var pNodes = htmlDoc.DocumentNode.SelectNodes("//p");
                        if (pNodes != null)
                        {
                            foreach (var pNode in pNodes)
                            {
                                var textNode = htmlDoc.CreateTextNode(pNode.InnerText + "\n\n");
                                pNode.ParentNode.ReplaceChild(textNode, pNode);
                            }
                        }

                        // replace li tags with "- " prefix and single newline suffix
                        var liNodes = htmlDoc.DocumentNode.SelectNodes("//li"); ;
                        if (liNodes != null)
                        {
                            foreach (var liNode in liNodes)
                            {
                                var textNode = htmlDoc.CreateTextNode("- " + liNode.InnerText + "\n");
                                liNode.ParentNode.ReplaceChild(textNode, liNode);
                            }
                        }

                        // replace i tags with "*" prefix and suffix
                        var iNodes = htmlDoc.DocumentNode.SelectNodes("//i"); ;
                        if (iNodes != null)
                        {
                            foreach (var iNode in iNodes)
                            {
                                var textNode = htmlDoc.CreateTextNode("*" + iNode.InnerText + "*");
                                iNode.ParentNode.ReplaceChild(textNode, iNode);
                            }
                        }

                        // replace b and strong tags with "**" prefix and suffix
                        var bNodes = htmlDoc.DocumentNode.SelectNodes("//b | //strong");
                        if (bNodes != null)
                        {
                            foreach (var bNode in bNodes)
                            {
                                var textNode = htmlDoc.CreateTextNode("**" + bNode.InnerText + "**");
                                bNode.ParentNode.ReplaceChild(textNode, bNode);
                            }
                        }

                        // replace u and underline tags with "__" prefix and suffix
                        var uNodes = htmlDoc.DocumentNode.SelectNodes("//u | //underline");
                        if (uNodes != null)
                        {
                            foreach (var uNode in uNodes)
                            {
                                var textNode = htmlDoc.CreateTextNode("__" + uNode.InnerText + "__");
                                uNode.ParentNode.ReplaceChild(textNode, uNode);
                            }
                        }

                        // replace all code tags with "`" prefix and suffix
                        var codeNodes = htmlDoc.DocumentNode.SelectNodes("//code"); ;
                        if (codeNodes != null)
                        {
                            foreach (var codeNode in codeNodes)
                            {
                                var textNode = htmlDoc.CreateTextNode("`" + codeNode.InnerText + "`");
                                codeNode.ParentNode.ReplaceChild(textNode, codeNode);
                            }
                        }

                        // replace all pre tags with "```" prefix and suffix
                        var preNodes = htmlDoc.DocumentNode.SelectNodes("//pre"); ;
                        if (preNodes != null)
                        {
                            foreach (var preNode in preNodes)
                            {
                                var textNode = htmlDoc.CreateTextNode("```\n" + preNode.InnerText + "\n```");
                                preNode.ParentNode.ReplaceChild(textNode, preNode);
                            }
                        }

                        // replace all remaining tags with their inner text
                        var allNodes = htmlDoc.DocumentNode.SelectNodes("//*"); ;
                        if (allNodes != null)
                        {
                            foreach (var node in allNodes)
                            {
                                if (node.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                                {
                                    var textNode = htmlDoc.CreateTextNode(node.InnerText);
                                    node.ParentNode.ReplaceChild(textNode, node);
                                }
                            }
                        }

                        return htmlDoc.DocumentNode.InnerText.Trim();
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// Retrieves a summary of task progress grouped by task type and status.
        /// </summary>
        /// <returns>A nested dictionary mapping task types to their status counts.</returns>
        /// <remarks>
        /// This method queries the Task_Queue table and aggregates the count of tasks for each combination of task type and status.
        /// </remarks>
        public async static Task<Dictionary<hasheous_server.Models.Tasks.TaskType, Dictionary<hasheous_server.Models.Tasks.QueueItemStatus, int>>> GetTaskProgressSummary()
        {
            string sql = "SELECT `task_name`, `status`, COUNT(`status`) AS `TypeCount` FROM `hasheous`.`Task_Queue` GROUP BY `task_name`, `status`;";
            DataTable dt = await Config.database.ExecuteCMDAsync(sql);

            Dictionary<hasheous_server.Models.Tasks.TaskType, Dictionary<hasheous_server.Models.Tasks.QueueItemStatus, int>> summary = new Dictionary<hasheous_server.Models.Tasks.TaskType, Dictionary<hasheous_server.Models.Tasks.QueueItemStatus, int>>();

            foreach (DataRow row in dt.Rows)
            {
                hasheous_server.Models.Tasks.TaskType taskType = (hasheous_server.Models.Tasks.TaskType)Enum.Parse(typeof(hasheous_server.Models.Tasks.TaskType), row["task_name"].ToString() ?? "Unknown");
                hasheous_server.Models.Tasks.QueueItemStatus status = (hasheous_server.Models.Tasks.QueueItemStatus)Enum.Parse(typeof(hasheous_server.Models.Tasks.QueueItemStatus), row["status"].ToString() ?? "Unknown");
                int count = Convert.ToInt32(row["TypeCount"]);

                if (!summary.ContainsKey(taskType))
                {
                    summary[taskType] = new Dictionary<hasheous_server.Models.Tasks.QueueItemStatus, int>();
                }
                summary[taskType][status] = count;
            }
            return summary;
        }
    }
}