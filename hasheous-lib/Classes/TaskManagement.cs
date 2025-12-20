using System.Data;
using Classes;
using hasheous_server.Models.Tasks;

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
        /// Retrieves the next available task for the specified client.
        /// </summary>
        /// <param name="clientAPIKey">The API key of the client.</param>
        /// <param name="publicId">The public client ID.</param>
        public async static Task ClientGetTask(string clientAPIKey, string publicId)
        {
            ClientModel? client = await GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
            if (client == null)
            {
                throw new Exception("Invalid client API key or public ID.");
            }
            throw new NotImplementedException();
        }

        /// <summary>
        /// Submits the status or result of a task from a client.
        /// </summary>
        /// <param name="clientAPIKey">The API key of the client.</param>
        /// <param name="publicId">The public client ID.</param>
        /// <param name="taskId">The ID of the task being reported.</param>
        /// <param name="result">The result or status of the task.</param>
        /// <param name="errorMessage">An optional error message if the task failed.</param>
        public async static Task ClientSubmitTaskStatusOrResult(string clientAPIKey, string publicId, string taskId, string result, string? errorMessage = null)
        {
            ClientModel? client = await GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
            if (client == null)
            {
                throw new Exception("Invalid client API key or public ID.");
            }
            throw new NotImplementedException();
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
        /// <param name="taskType">The type of the task to enqueue.</param>
        /// <param name="capabilities">A list of capabilities required to process the task.</param>
        /// <param name="parameters">A string containing task-specific parameters.</param>
        /// <returns>The enqueued <see cref="QueueItemModel"/> instance.</returns>
        public static QueueItemModel EnqueueTask(TaskType taskType, List<Capabilities> capabilities, string parameters)
        {
            QueueItemModel task = new QueueItemModel(taskType, capabilities, parameters);
            return task;
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
            DataTable dt = Config.database.ExecuteCMD("SELECT * FROM `Task_Queue`;");
            List<QueueItemModel> tasks = new List<QueueItemModel>();
            foreach (DataRow row in dt.Rows)
            {
                tasks.Add(new QueueItemModel(row));
            }
            return tasks;
        }

        /// <summary>
        /// Assigns a task to a specific client by their IDs.
        /// </summary>
        /// <param name="taskId">The ID of the task to assign.</param>
        /// <param name="clientId">The ID of the client to assign the task to.</param>
        public static void AssignTaskToClient(long taskId, long clientId)
        {
            var task = GetTask(taskId);
            if (task == null)
            {
                throw new Exception("Task not found.");
            }
            task.ClientId = clientId;
            _ = task.Commit();
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
    }
}