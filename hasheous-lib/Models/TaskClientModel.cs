using System.Data;
using Classes;

namespace hasheous_server.Models.Tasks
{
    /// <summary>
    /// Represents a client that interacts with the task orchestration system.
    /// </summary>
    public class ClientModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientModel"/> class.
        /// </summary>
        /// <remarks>
        /// Default constructor for creating an empty client model.
        /// </remarks>
        public ClientModel() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientModel"/> class using data from the specified <see cref="DataRow"/>.
        /// </summary>
        /// <param name="row">The <see cref="DataRow"/> containing client data.</param>
        /// <remarks>
        /// This constructor is typically used for deserializing client data from a database.
        /// </remarks>
        public ClientModel(DataRow row)
        {
            Refresh(row);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientModel"/> class with the specified API key, client name, owner ID, and optional version.
        /// </summary>
        /// <param name="clientAPIKey">The API key assigned to the client.</param>
        /// <param name="clientName">The name of the client.</param>
        /// <param name="ownerId">The unique identifier of the owner for the client.</param>
        /// <param name="version">The version string of the client application (optional).</param>
        /// <remarks>
        /// This constructor is typically used when registering a new client.
        /// </remarks>
        public ClientModel(string clientAPIKey, string clientName, string ownerId, string? version = null)
        {
            this._PublicId = Guid.NewGuid();
            this.APIKey = clientAPIKey;
            this.ClientName = clientName;
            this._OwnerId = ownerId;
            this.ClientVersion = version;
            this._CreatedAt = DateTime.UtcNow;
            this._LastContactAt = DateTime.UtcNow;

            // Id will be set when saved to database
            DataTable dt = Config.database.ExecuteCMD("INSERT INTO Task_Clients (public_id, api_key, client_name, owner_id, created_at, last_contact_at, version) VALUES (@public_id, @api_key, @client_name, @owner_id, @created_at, @last_contact_at, @version); SELECT LAST_INSERT_ID();", new Dictionary<string, object>
            {
                { "@public_id", this._PublicId.ToString() },
                { "@api_key", this._APIKey ?? "" },
                { "@client_name", this.ClientName },
                { "@owner_id", this.OwnerId },
                { "@created_at", this._CreatedAt },
                { "@last_contact_at", this._LastContactAt },
                { "@version", this._Version ?? "" }
            });
            this._Id = Convert.ToInt64(dt.Rows[0][0]);
        }

        /// <summary>
        /// Gets the unique identifier for the client.
        /// </summary>
        public long Id { get; }
        private long _Id { get; set; }

        /// <summary>
        /// Gets the public unique identifier for the client.
        /// </summary>
        public Guid PublicId { get { return _PublicId; } }
        private Guid _PublicId { get; set; }

        /// <summary>
        /// Gets or sets the name of the client.
        /// </summary>
        public string ClientName { get; set; } = "";

        /// <summary>
        /// Gets the unique identifier of the owner for the client.
        /// </summary>
        public string OwnerId { get { return _OwnerId; } }
        private string _OwnerId { get; set; } = "";

        /// <summary>
        /// Sets the API key assigned to the client.
        /// </summary>
        public string? APIKey { set { _APIKey = value; } }
        private string? _APIKey { get; set; }

        /// <summary>
        /// Checks whether the provided API key matches the client's stored API key.
        /// </summary>
        /// <param name="apiKey">The API key to validate against the client's stored API key.</param>
        /// <returns>True if the API key matches; otherwise, false.</returns>
        public bool CheckAPIKey(string apiKey)
        {
            if (this._APIKey == null)
            {
                return false;
            }
            else
            {
                if (this._APIKey == apiKey)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the date and time when the client was created.
        /// </summary>
        public DateTime CreatedAt { get; }
        private DateTime _CreatedAt { get; set; }

        /// <summary>
        /// Gets the date and time when the client last contacted the server.
        /// </summary>
        public DateTime LastContactAt { get; }
        private DateTime _LastContactAt { get; set; }

        /// <summary>
        /// Gets a value indicating whether the client is considered active (contacted within the last 5 minutes).
        /// </summary>
        public bool IsActive
        {
            get
            {
                return (DateTime.UtcNow - _LastContactAt).TotalMinutes < 5;
            }
        }

        /// <summary>
        /// Gets or sets the version string of the client application.
        /// </summary>
        public string? ClientVersion
        {
            get
            {
                return _Version;
            }
            set
            {
                // If null, set to null
                if (value == null)
                {
                    _Version = null;
                }
                else
                {
                    // parse version string to Version object
                    if (Version.TryParse(value, out var parsedVersion))
                    {
                        _Version = parsedVersion.ToString();
                    }
                    else
                    {
                        _Version = null;
                    }
                }
            }
        }
        private string? _Version { get; set; }

        /// <summary>
        /// Gets or sets the list of task types that the client is capable of handling.
        /// </summary>
        public List<Capabilities> Capabilities { get; set; } = new List<Capabilities>();

        /// <summary>
        /// Refreshes the current client model instance with data from the specified DataRow or from the database if no row is provided.
        /// </summary>
        /// <param name="row">Optional DataRow containing client data; if null, data is loaded from the database using the client's Id.</param>
        /// <returns>The refreshed <see cref="ClientModel"/> instance.</returns>
        public ClientModel Refresh(DataRow? row = null)
        {
            if (row == null)
            {
                DataTable dt = Config.database.ExecuteCMD("SELECT * FROM Task_Clients WHERE id = @id", new Dictionary<string, object>
                { { "@id", this.Id } });

                if (dt.Rows.Count == 0)
                {
                    throw new Exception("Client not found in database.");
                }

                row = dt.Rows[0];
            }

            this._Id = row.Field<long>("id");
            this._PublicId = Guid.Parse(row.Field<string>("public_id") ?? Guid.Empty.ToString());
            this.ClientName = row.Field<string>("client_name") ?? "";
            this._OwnerId = row.Field<string>("owner_id") ?? "";
            this._APIKey = row.Field<string>("api_key") ?? "";
            this._CreatedAt = row.Field<DateTime>("created_at");
            this._LastContactAt = row.Field<DateTime>("last_contact_at");
            this.ClientVersion = row.Field<string>("version") ?? "";
            var capabilitiesJson = row.Field<string>("capabilities");
            this.Capabilities = string.IsNullOrEmpty(capabilitiesJson)
                ? new List<Capabilities>()
                : System.Text.Json.JsonSerializer.Deserialize<List<Capabilities>>(capabilitiesJson) ?? new List<Capabilities>();

            return this;
        }

        /// <summary>
        /// Unregisters the client by removing it from the database and clearing its properties.
        /// </summary>
        public void Unregister()
        {
            // update the database to remove this client
            Config.database.ExecuteCMD("DELETE FROM Task_Clients WHERE id = @id", new Dictionary<string, object>
            { { "@id", this.Id } });

            // clear this object
            this._Id = 0;
            this.ClientName = "";
            this._OwnerId = "";
            this._APIKey = null;
            this._CreatedAt = DateTime.MinValue;
            this._LastContactAt = DateTime.MinValue;
            this.ClientVersion = null;
            this.Capabilities = new List<Capabilities>();
        }

        /// <summary>
        /// Updates the client's last contact time to the current UTC time and persists the change to the database.
        /// </summary>
        public void Heartbeat()
        {
            this._LastContactAt = DateTime.UtcNow;

            Config.database.ExecuteCMD("UPDATE Task_Clients SET last_contact_at = @last_contact_at WHERE id = @id", new Dictionary<string, object>
            {
                { "@last_contact_at", this._LastContactAt },
                { "@id", this._Id }
            });
        }

        /// <summary>
        /// Commits any changes made to the client model to the database.
        /// </summary>
        public void Commit()
        {
            if (this._Id == 0)
            {
                throw new Exception("Cannot commit a client that has not been registered.");
            }

            Config.database.ExecuteCMD("UPDATE Task_Clients SET client_name = @client_name, api_key = @api_key, last_heartbeat = @last_heartbeat, version = @version, capabilities = @capabilities WHERE id = @id", new Dictionary<string, object>
            {
                { "@client_name", this.ClientName },
                { "@api_key", this._APIKey ?? "" },
                { "@last_heartbeat", this._LastContactAt },
                { "@version", this._Version ?? "" },
                { "@capabilities", System.Text.Json.JsonSerializer.Serialize(this.Capabilities) },
                { "@id", this._Id }
            });
        }
    }
}