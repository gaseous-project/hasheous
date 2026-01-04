
namespace hasheous_taskrunner.Classes.Capabilities
{
    /// <summary>
    /// Represents a capability that checks disk space on the host and returns usage/configuration details.
    /// </summary>
    public class DiskSpaceCapability : ICapability
    {
        /// <inheritdoc/>
        public int CapabilityId => 10;

        /// <inheritdoc/>
        public bool IsInternalCapability => false;

        /// <inheritdoc/>
        public Dictionary<string, object>? Configuration
        {
            get
            {
                return _configuration;
            }
            set
            {
                if (_configuration == null || _configuration.Count == 0)
                {
                    Dictionary<string, object> configDict = value ?? new Dictionary<string, object>();
                    int minimumFreeSpaceMb = 1024;

                    if (value != null)
                    {
                        if (value.ContainsKey("minimum_free_space_mb"))
                        {
                            try
                            {
                                minimumFreeSpaceMb = Convert.ToInt32(value["minimum_free_space_mb"]);
                            }
                            catch
                            {
                                minimumFreeSpaceMb = 1024;
                            }
                        }
                    }

                    configDict["minimum_free_space_mb"] = minimumFreeSpaceMb;
                    _configuration = configDict;
                }
            }
        }

        private Dictionary<string, object>? _configuration;

        /// <inheritdoc/>
        public async Task<Dictionary<string, object>?> ExecuteAsync(Dictionary<string, object> parameters)
        {
            bool result = await TestAsync();
            return new Dictionary<string, object> { { "result", result } };
        }

        /// <inheritdoc/>
        public async Task<bool> TestAsync()
        {
            // check disk space on the host
            DriveInfo drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "/");
            long availableFreeSpaceMb = drive.AvailableFreeSpace / (1024 * 1024);
            int minimumFreeSpaceMb = 1024;

            if (this.Configuration != null && this.Configuration.ContainsKey("minimum_free_space_mb"))
            {
                minimumFreeSpaceMb = Convert.ToInt32(this.Configuration["minimum_free_space_mb"]);
            }

            Console.WriteLine($"DiskSpaceCapability: Available free space: {availableFreeSpaceMb} MB, Minimum required: {minimumFreeSpaceMb} MB");

            return availableFreeSpaceMb >= minimumFreeSpaceMb;
        }
    }
}