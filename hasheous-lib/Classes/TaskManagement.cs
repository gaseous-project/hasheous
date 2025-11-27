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
        /// <returns>A dictionary containing the client API key and public client ID.</returns>
        public static async Task<Dictionary<string, string>> RegisterClient(string userAPIKey, string clientName, string version)
        {
            // resolve userAPIKey to user account
            var user = GetUserObjectFromAPIKey(userAPIKey);

            // create new client
            string clientAPIKey = GenerateClientAPIKey();
            ClientModel client = new ClientModel(clientAPIKey, clientName, user.Id.ToString(), version);
            Dictionary<string, string> response = new Dictionary<string, string>
            {
                { "client_api_key", clientAPIKey },
                { "client_id", client.PublicId.ToString() }
            };

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
        public static ClientModel? GetClient(string userAPIKey, string publicId)
        {
            // resolve userAPIKey to user account
            var user = GetUserObjectFromAPIKey(userAPIKey);

            DataTable dt = db.ExecuteCMD("SELECT * FROM Task_Clients WHERE public_id = @public_id AND owner_id = @owner_id LIMIT 1;", new Dictionary<string, object>
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
        public static ClientModel? GetClientByAPIKeyAndPublicId(string clientAPIKey, string publicId)
        {
            DataTable dt = db.ExecuteCMD("SELECT * FROM Task_Clients WHERE api_key = @api_key AND public_id = @public_id LIMIT 1;", new Dictionary<string, object>
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
        public static List<ClientModel> GetAllClientsForUser(string userAPIKey)
        {
            // resolve userAPIKey to user account
            var user = GetUserObjectFromAPIKey(userAPIKey);

            List<ClientModel> clients = new List<ClientModel>();
            DataTable dt = db.ExecuteCMD("SELECT * FROM Task_Clients WHERE owner_id = @owner_id;", new Dictionary<string, object>
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
        public static List<ClientModel> GetAllClients()
        {
            List<ClientModel> clients = new List<ClientModel>();
            DataTable dt = db.ExecuteCMD("SELECT * FROM Task_Clients;");
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
        public static void UnregisterClient(string userAPIKey, string publicId)
        {
            var user = GetUserObjectFromAPIKey(userAPIKey);

            db.ExecuteCMD("DELETE FROM Task_Clients WHERE owner_id = @owner_id AND public_id = @public_id;", new Dictionary<string, object>
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
        public static void Heartbeat(string clientAPIKey, string publicId)
        {
            ClientModel? client = GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
            if (client == null)
            {
                throw new Exception("Invalid client API key or public ID.");
            }
            client.Heartbeat();
        }

        /// <summary>
        /// Updates the properties of a client, such as name, version, and capabilities.
        /// </summary>
        /// <param name="clientAPIKey">The API key of the client.</param>
        /// <param name="publicId">The public client ID.</param>
        /// <param name="clientName">The new name of the client application (optional).</param>
        /// <param name="version">The new version of the client application (optional).</param>
        /// <param name="capabilities">The new list of supported task types for the client (optional).</param>
        /// <exception cref="Exception">Thrown if the client API key or public ID is invalid.</exception>
        public static void UpdateClient(string clientAPIKey, string publicId, string? clientName = null, string? version = null, List<TaskType>? capabilities = null)
        {
            ClientModel? client = GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
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

            client.Commit();
        }

        /// <summary>
        /// Retrieves the next available task for the specified client.
        /// </summary>
        /// <param name="clientAPIKey">The API key of the client.</param>
        /// <param name="publicId">The public client ID.</param>
        public static void ClientGetTask(string clientAPIKey, string publicId)
        {
            ClientModel? client = GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
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
        public static void ClientSubmitTaskStatusOrResult(string clientAPIKey, string publicId, string taskId, string result, string? errorMessage = null)
        {
            ClientModel? client = GetClientByAPIKeyAndPublicId(clientAPIKey, publicId);
            if (client == null)
            {
                throw new Exception("Invalid client API key or public ID.");
            }
            throw new NotImplementedException();
        }

        private static string GenerateClientAPIKey()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 128);
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
        private static Database db = new Database(Database.databaseType.MySql, Config.DatabaseConfiguration.ConnectionString);

    }
}